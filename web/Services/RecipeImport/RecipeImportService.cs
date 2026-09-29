using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using web.Services.Ollama;
using web.Services.Ollama.Dto;
using web.Services.Ollama.Interfaces;

namespace web.Services.RecipeImport;

public record ScannedIngredient(string Amount, string Unit, string Name);

public record RecipeScanResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public bool Found { get; init; }
    public string? Source { get; init; }
    public string? Title { get; init; }
    public string? Category { get; init; }
    public int? PrepTime { get; init; }
    public int? CookTime { get; init; }
    public int? Servings { get; init; }
    public string? Difficulty { get; init; }
    public List<ScannedIngredient> Ingredients { get; init; } = new();
    public List<string> Steps { get; init; } = new();

    public static RecipeScanResult Fail(string error) => new() { Ok = false, Error = error };
    public static RecipeScanResult NotFound() => new() { Ok = true, Found = false };
}

// Henter en opskriftsside og udtrækker opskriften: først fra Schema.org JSON-LD (præcist og
// hurtigt), og ellers ved at sende sidens tekst til Ollama-modellen.
public class RecipeImportService
{
    public const string HttpClientName = "RecipeImport";

    // Én fast num_ctx, da Ollama genindlæser modellen hver gang den ændres. Overskrides den,
    // afkorter Ollama stille prompten til ca. det halve – derfor holdes sideteksten godt under.
    private const int AiNumCtx = 16384;
    private const int AiNumPredict = 3072;
    private const int AiMaxPageChars = 24000;
    private const int AiCharsBeforeIngredientHeading = 3000;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOllamaService _ollama;
    private readonly IOllamaConfigurationProvider _ollamaConfig;

    public RecipeImportService(IHttpClientFactory httpClientFactory, IOllamaService ollama, IOllamaConfigurationProvider ollamaConfig)
    {
        _httpClientFactory = httpClientFactory;
        _ollama = ollama;
        _ollamaConfig = ollamaConfig;
    }

    public async Task<RecipeScanResult> ScanUrlAsync(Uri url, IReadOnlyCollection<string> existingCategories, CancellationToken cancellationToken)
    {
        string html;
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            html = DetectHtmlEncoding(bytes, response.Content.Headers.ContentType?.CharSet).GetString(bytes);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RecipeScanResult.Fail("Siden tog for lang tid at svare (timeout efter 45 sek). Prøv igen.");
        }
        catch (HttpRequestException ex)
        {
            return RecipeScanResult.Fail($"Kunne ikke hente siden: {ex.Message}");
        }

        return await ScanHtmlAsync(html, existingCategories, cancellationToken);
    }

    public async Task<RecipeScanResult> ScanHtmlAsync(string html, IReadOnlyCollection<string> existingCategories, CancellationToken cancellationToken)
    {
        var structured = TryExtractJsonLdRecipe(html, existingCategories);
        if (structured != null)
            return structured;

        var pageText = ExtractPageTextForAi(html);
        if (pageText.Length < 100)
            return RecipeScanResult.Fail("Siden returnerede ikke nok tekstindhold til analyse");

        return await ExtractWithAiAsync(pageText, existingCategories, cancellationToken);
    }

    // ── Schema.org JSON-LD ──────────────────────────────────────────────────

    private static readonly Regex ScriptTagRx = new(@"<script\b([^>]*)>(.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly JsonDocumentOptions LenientJson = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    private static RecipeScanResult? TryExtractJsonLdRecipe(string html, IReadOnlyCollection<string> existingCategories)
    {
        foreach (Match m in ScriptTagRx.Matches(html))
        {
            // ASP.NET-sider (fx Arla) HTML-koder attributten: type="application/ld&#x2B;json"
            if (!WebUtility.HtmlDecode(m.Groups[1].Value).Contains("application/ld+json", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(m.Groups[2].Value, LenientJson);
                foreach (var node in FindRecipeNodes(doc.RootElement))
                {
                    var result = FromSchemaRecipe(node, existingCategories);
                    if (result != null)
                        return result;
                }
            }
            catch (JsonException) { /* ugyldig JSON-LD – prøv næste blok */ }
        }
        return null;
    }

    private static IEnumerable<JsonElement> FindRecipeNodes(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            if (IsSchemaType(el, "@type", "Recipe") || IsSchemaType(el, "type", "Recipe"))
            {
                yield return el;
                yield break;
            }
            foreach (var prop in el.EnumerateObject())
                foreach (var node in FindRecipeNodes(prop.Value))
                    yield return node;
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
                foreach (var node in FindRecipeNodes(item))
                    yield return node;
        }
    }

    private static bool IsSchemaType(JsonElement el, string key, string type)
    {
        if (!el.TryGetProperty(key, out var t)) return false;
        return t.ValueKind switch
        {
            JsonValueKind.String => string.Equals(t.GetString(), type, StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => t.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String &&
                                       string.Equals(x.GetString(), type, StringComparison.OrdinalIgnoreCase)),
            _ => false,
        };
    }

    private static RecipeScanResult? FromSchemaRecipe(JsonElement recipe, IReadOnlyCollection<string> existingCategories)
    {
        var ingredientLines = new List<string>();
        if ((recipe.TryGetProperty("recipeIngredient", out var ingsEl) || recipe.TryGetProperty("ingredients", out ingsEl)) &&
            ingsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var ing in ingsEl.EnumerateArray())
                if (ing.ValueKind == JsonValueKind.String)
                    ingredientLines.Add(HtmlToText(ing.GetString()!));
        }

        var ingredients = BuildIngredients(ingredientLines);
        var steps = recipe.TryGetProperty("recipeInstructions", out var instrEl) ? ReadInstructions(instrEl) : new List<string>();
        if (ingredients.Count == 0 && steps.Count == 0)
            return null;

        var (prep, cook) = NormalizeTimes(
            ParseIsoDuration(GetString(recipe, "prepTime")),
            ParseIsoDuration(GetString(recipe, "cookTime")),
            ParseIsoDuration(GetString(recipe, "totalTime")));

        return new RecipeScanResult
        {
            Ok = true,
            Found = true,
            Source = "structured",
            Title = GetString(recipe, "name") ?? GetString(recipe, "headline"),
            Category = MatchCategory(GetStrings(recipe, "recipeCategory"), existingCategories),
            PrepTime = prep,
            CookTime = cook,
            Servings = ParseYield(recipe),
            Ingredients = ingredients,
            Steps = steps,
        };
    }

    // recipeInstructions kan være en tekst, en liste af tekster, HowToStep-objekter eller
    // HowToSection-objekter (fx Arlas "Kødboller"/"Karrysauce") med trinene i itemListElement.
    // Har opskriften flere navngivne afsnit, får første trin i hvert afsnit afsnittets navn foran,
    // så opdelingen ikke går tabt i appens flade trinliste.
    private static List<string> ReadInstructions(JsonElement instructions)
    {
        var sections = new List<(string? Name, List<string> Steps)> { (null, new List<string>()) };

        void Walk(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.String:
                    sections[^1].Steps.AddRange(SplitSteps(e.GetString()!));
                    break;
                case JsonValueKind.Array:
                    foreach (var item in e.EnumerateArray()) Walk(item);
                    break;
                case JsonValueKind.Object when e.TryGetProperty("itemListElement", out var items):
                    sections.Add((GetString(e, "name"), new List<string>()));
                    Walk(items);
                    sections.Add((null, new List<string>()));
                    break;
                case JsonValueKind.Object:
                    if ((GetString(e, "text") ?? GetString(e, "name")) is { } text)
                        sections[^1].Steps.AddRange(SplitSteps(text));
                    break;
            }
        }

        Walk(instructions);

        var nonEmpty = sections.Where(s => s.Steps.Count > 0).ToList();
        var prefixSections = nonEmpty.Count(s => !string.IsNullOrWhiteSpace(s.Name)) > 1;
        var steps = new List<string>();
        foreach (var (name, sectionSteps) in nonEmpty)
        {
            for (var i = 0; i < sectionSteps.Count; i++)
                steps.Add(prefixSections && i == 0 && !string.IsNullOrWhiteSpace(name) ? $"{name}: {sectionSteps[i]}" : sectionSteps[i]);
        }
        return steps;
    }

    private static readonly Regex LeadingStepNumberRx = new(@"^\d+\s*[.)]\s+", RegexOptions.Compiled);

    private static IEnumerable<string> SplitSteps(string raw) =>
        HtmlToText(raw)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => LeadingStepNumberRx.Replace(s, ""))
            .Where(s => s.Length > 0);

    private static int? ParseYield(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("recipeYield", out var y)) return null;
        var first = y.ValueKind == JsonValueKind.Array ? y.EnumerateArray().FirstOrDefault() : y;
        var text = first.ValueKind switch
        {
            JsonValueKind.String => first.GetString(),
            JsonValueKind.Number => first.GetRawText(),
            _ => null,
        };
        var m = text == null ? null : Regex.Match(text, @"\d+");
        return m is { Success: true } && int.TryParse(m.Value, out var n) && n > 0 ? n : null;
    }

    private static readonly Regex IsoDurationRx = new(@"^P(?:(?<d>\d+)D)?T?(?:(?<h>\d+)H)?(?:(?<m>\d+)M)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ISO 8601: PT45M → 45, PT1H30M → 90, PT00M → null
    private static int? ParseIsoDuration(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return null;
        var m = IsoDurationRx.Match(iso.Trim());
        if (!m.Success) return null;
        int Part(string g) => m.Groups[g].Success ? int.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture) : 0;
        var total = Part("d") * 1440 + Part("h") * 60 + Part("m");
        return total > 0 ? total : null;
    }

    // ── AI-udtræk via Ollama ────────────────────────────────────────────────

    private static readonly JsonElement AiResponseSchema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {
            "found":       { "type": "boolean" },
            "title":       { "type": ["string", "null"] },
            "category":    { "type": ["string", "null"] },
            "prepTime":    { "type": ["integer", "null"] },
            "cookTime":    { "type": ["integer", "null"] },
            "totalTime":   { "type": ["integer", "null"] },
            "servings":    { "type": ["integer", "null"] },
            "difficulty":  { "type": ["string", "null"], "enum": ["Let", "Middel", "Svær", null] },
            "ingredients": { "type": "array", "items": { "type": "string" } },
            "steps":       { "type": "array", "items": { "type": "string" } }
          },
          "required": ["found", "title", "category", "prepTime", "cookTime", "totalTime", "servings", "difficulty", "ingredients", "steps"]
        }
        """);

    private static string BuildSystemPrompt(IReadOnlyCollection<string> existingCategories)
    {
        var categories = existingCategories.Count > 0 ? string.Join(", ", existingCategories) : "(ingen)";
        return $$"""
            Du udtrækker opskrifter fra teksten på en webside. Linjer der starter med "- " er punkter i en liste, og linjer der starter med "## " er overskrifter. Teksten kan indeholde andet indhold (historier, reklamer, kommentarer) før og efter selve opskriften – find opskriften uanset hvor den står.

            Regler:
            - Udtræk KUN det der faktisk står i teksten. Gæt, estimér eller opfind aldrig værdier. Står en værdi ikke i teksten, skal feltet være null.
            - found: true hvis teksten indeholder en opskrift med ingredienser eller fremgangsmåde, ellers false.
            - title: opskriftens navn som det står skrevet.
            - ingredients: én tekstlinje for HVER ingrediens i opskriften, i samme rækkefølge – også når samme ingrediens optræder flere gange (fx i både fars og sovs). Skriv mængde, enhed og navn i den rækkefølge, og afskriv mængden præcis som den står (fx "300 g hakket grise- og kalvekød", "1 æg", "friskkværnet peber"). Overskrifter der inddeler ingredienslisten (fx "Kødboller" eller "Til servering") er IKKE ingredienser.
            - steps: hvert trin i fremgangsmåden i rækkefølge, uden nummerering. Gengiv teksten ordret – omskriv, forkort eller slå ikke trin sammen.
            - prepTime, cookTime, totalTime: minutter som heltal, kun hvis siden angiver dem. Angiver siden kun én samlet tid, så brug totalTime.
            - servings: antal portioner/personer som heltal, kun hvis siden angiver det. Et antal stykker (fx "ca. 24 stk") er ikke portioner.
            - difficulty: kun hvis siden angiver en sværhedsgrad; brug da Let, Middel eller Svær.
            - category: vælg den bedst passende af disse eksisterende kategorier: {{categories}}. Passer ingen af dem, så brug sidens egen kategori hvis den angiver en, ellers null.
            """;
    }

    private async Task<RecipeScanResult> ExtractWithAiAsync(string pageText, IReadOnlyCollection<string> existingCategories, CancellationToken cancellationToken)
    {
        var settings = await _ollamaConfig.GetActiveConfigurationAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.DefaultChatModel))
            return RecipeScanResult.Fail("Ingen chat-model er konfigureret (Ollama:DefaultChatModel i appsettings.json)");

        var request = new OllamaChatRequest
        {
            Model = settings.DefaultChatModel,
            Stream = false,
            // JSON-schema tvinger modellen til præcis denne struktur (Ollama structured outputs)
            Format = AiResponseSchema,
            // Tænkning giver ikke bedre udtræk her, men koster mange ekstra tokens/sekunder
            Think = JsonSerializer.SerializeToElement(false),
            Options = new OllamaRuntimeOptionsDto { Temperature = 0, NumCtx = AiNumCtx, NumPredict = AiNumPredict },
            Messages = new List<OllamaChatMessageDto>
            {
                new() { Role = "system", Content = BuildSystemPrompt(existingCategories) },
                new() { Role = "user", Content = "Sidetekst:\n\n" + pageText },
            },
        };

        OllamaChatResponse response;
        try
        {
            response = await _ollama.ChatAsync(request, cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return RecipeScanResult.Fail("Ollama svarede ikke i tide. Modellen er måske ved at vågne – prøv igen om et øjeblik.");
        }
        catch (OllamaException ex)
        {
            return RecipeScanResult.Fail($"Ollama fejl: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return RecipeScanResult.Fail($"Kunne ikke kontakte Ollama: {ex.Message}");
        }

        if (response.DoneReason == "length")
            return RecipeScanResult.Fail("AI-svaret blev afbrudt, fordi opskriften var for lang. Prøv igen, eller indtast opskriften manuelt.");

        return ParseAiResponse(response.Message?.Content, existingCategories);
    }

    private static RecipeScanResult ParseAiResponse(string? content, IReadOnlyCollection<string> existingCategories)
    {
        if (string.IsNullOrWhiteSpace(content))
            return RecipeScanResult.Fail("Modellen returnerede et tomt svar");

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (!root.TryGetProperty("found", out var found) || found.ValueKind != JsonValueKind.True)
                return RecipeScanResult.NotFound();

            var ingredients = BuildIngredients(GetStringArray(root, "ingredients"));
            var steps = GetStringArray(root, "steps")
                .Select(s => LeadingStepNumberRx.Replace(s.Trim(), ""))
                .Where(s => s.Length > 0)
                .ToList();
            if (ingredients.Count == 0 && steps.Count == 0)
                return RecipeScanResult.NotFound();

            var (prep, cook) = NormalizeTimes(GetPositiveInt(root, "prepTime"), GetPositiveInt(root, "cookTime"), GetPositiveInt(root, "totalTime"));
            var difficulty = GetString(root, "difficulty");

            return new RecipeScanResult
            {
                Ok = true,
                Found = true,
                Source = "ai",
                Title = GetString(root, "title"),
                Category = MatchCategory(new[] { GetString(root, "category") }, existingCategories),
                PrepTime = prep,
                CookTime = cook,
                Servings = GetPositiveInt(root, "servings"),
                Difficulty = difficulty is "Let" or "Middel" or "Svær" ? difficulty : null,
                Ingredients = ingredients,
                Steps = steps,
            };
        }
        catch (JsonException)
        {
            return RecipeScanResult.Fail("Kunne ikke fortolke AI-svaret som JSON. Prøv at scanne igen.");
        }
    }

    // ── Sidetekst til AI ────────────────────────────────────────────────────

    private const RegexOptions HtmlRx = RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled;
    private static readonly Regex NoiseBlocksRx = new(@"<(script|style|noscript|nav|footer|aside|svg|iframe|template|button|select)\b[^>]*>.*?</\1>", HtmlRx);
    private static readonly Regex CommentRx = new(@"<!--.*?-->", HtmlRx);
    private static readonly Regex MainRx = new(@"<main\b[^>]*>(.*)</main>", HtmlRx);
    private static readonly Regex ArticleRx = new(@"<article\b[^>]*>(.*)</article>", HtmlRx);
    private static readonly Regex BrRx = new(@"<br\s*/?>", HtmlRx);
    private static readonly Regex ListItemRx = new(@"<li\b[^>]*>", HtmlRx);
    private static readonly Regex HeadingRx = new(@"<h[1-6]\b[^>]*>", HtmlRx);
    private static readonly Regex BlockTagRx = new(@"</?(p|div|ul|ol|li|h[1-6]|tr|table|section|article|dd|dt|dl|figure|figcaption|blockquote)\b[^>]*>", HtmlRx);
    private static readonly Regex CellTagRx = new(@"</?(td|th)\b[^>]*>", HtmlRx);
    private static readonly Regex AnyTagRx = new(@"<[^>]+>", HtmlRx);
    private static readonly Regex HorizontalSpaceRx = new(@"[ \t ]+", RegexOptions.Compiled);
    private static readonly Regex IngredientHeadingRx = new(@"^(##\s*)?(ingredienser(ne)?|ingredients|du skal bruge)\s*:?\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    // Bevarer sidens struktur (linjeskift, "- " for listepunkter, "## " for overskrifter), så modellen
    // kan se hvilke linjer der hører sammen – uden det blev hele siden én lang tekstlinje, hvor fx
    // "1 dl mælk" og "2 dl mælk" fra hver sit afsnit let blev forvekslet.
    private static string ExtractPageTextForAi(string html)
    {
        html = NoiseBlocksRx.Replace(html, " ");
        html = CommentRx.Replace(html, " ");

        var main = MainRx.Match(html);
        if (!main.Success) main = ArticleRx.Match(html);
        if (main.Success && AnyTagRx.Replace(main.Groups[1].Value, "").Trim().Length > 500)
            html = main.Groups[1].Value;

        html = BrRx.Replace(html, "\n");
        html = ListItemRx.Replace(html, "\n- ");
        html = HeadingRx.Replace(html, "\n\n## ");
        html = BlockTagRx.Replace(html, "\n");
        html = CellTagRx.Replace(html, " ");
        html = AnyTagRx.Replace(html, " ");
        html = WebUtility.HtmlDecode(html);

        var sb = new StringBuilder();
        var lastWasBlank = true;
        foreach (var rawLine in html.Split('\n'))
        {
            var line = HorizontalSpaceRx.Replace(rawLine, " ").Trim();
            if (line is "" or "-" or "##")
            {
                if (!lastWasBlank) sb.Append('\n');
                lastWasBlank = true;
                continue;
            }
            sb.Append(line).Append('\n');
            lastWasBlank = false;
        }
        var text = sb.ToString().Trim();

        // Send kun et vindue omkring ingredienslisten: lange sider (FAQ, kommentarer) sprænger ellers
        // kontekstvinduet, og så skærer Ollama netop den del væk hvor opskriften står.
        var heading = IngredientHeadingRx.Match(text);
        var start = heading.Success ? Math.Max(0, heading.Index - AiCharsBeforeIngredientHeading) : 0;
        return text.Substring(start, Math.Min(AiMaxPageChars, text.Length - start));
    }

    private static Encoding DetectHtmlEncoding(byte[] bytes, string? headerCharSet)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8;

        // Mange sider angiver kun charset i <meta>, ikke i HTTP-headeren
        var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 2048));
        var meta = Regex.Match(head, @"<meta[^>]+charset\s*=\s*[""']?([a-zA-Z0-9_\-]+)", RegexOptions.IgnoreCase);

        return TryGetEncoding(headerCharSet?.Trim('"', '\''))
            ?? (meta.Success ? TryGetEncoding(meta.Groups[1].Value) : null)
            ?? Encoding.UTF8;
    }

    private static Encoding? TryGetEncoding(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        try { return Encoding.GetEncoding(name); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { return null; }
    }

    // ── Normalisering (fælles for JSON-LD og AI) ────────────────────────────

    private static (int? Prep, int? Cook) NormalizeTimes(int? prep, int? cook, int? total)
    {
        if (total is > 0)
        {
            // Kun samlet tid kendt: danske sider kalder den typisk "tilberedningstid"
            if (prep is null && cook is null) cook = total;
            else if (cook is null && total > prep) cook = total - prep;
            else if (prep is null && total > cook) prep = total - cook;
        }
        return (prep, cook);
    }

    private static string? MatchCategory(IEnumerable<string?> candidates, IReadOnlyCollection<string> existingCategories)
    {
        var names = candidates
            .SelectMany(c => (c ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
        foreach (var name in names)
        {
            var existing = existingCategories.FirstOrDefault(e => string.Equals(e, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;
        }
        return names.FirstOrDefault();
    }

    // Overskrift for en del-opskrift, fx "Kødboller - ca. 24 stk" – ikke en ingrediens
    private static readonly Regex SectionHeadingLineRx = new(@"-\s*ca\.\s*\d+\s*(stk|styk|portion(er)?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static List<ScannedIngredient> BuildIngredients(IEnumerable<string> lines) =>
        MergeDuplicateIngredients(lines
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !SectionHeadingLineRx.IsMatch(l))
            .Select(ParseIngredientLine)
            .ToList());

    private static readonly Dictionary<char, decimal> VulgarFractions = new()
    {
        ['½'] = 0.5m, ['⅓'] = 1m / 3m, ['⅔'] = 2m / 3m,
        ['¼'] = 0.25m, ['¾'] = 0.75m,
        ['⅕'] = 0.2m, ['⅖'] = 0.4m, ['⅗'] = 0.6m, ['⅘'] = 0.8m,
        ['⅙'] = 1m / 6m, ['⅚'] = 5m / 6m,
        ['⅛'] = 0.125m, ['⅜'] = 0.375m, ['⅝'] = 0.625m, ['⅞'] = 0.875m,
    };

    private static readonly HashSet<string> KnownUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "g", "gram", "kg", "dl", "l", "liter", "cl", "ml",
        "tsk", "spsk", "sp", "ss", "stk", "styk", "cm",
        "dåse", "dåser", "ds", "fed", "bundt", "bdt", "knivspids", "knsp",
        "pose", "poser", "blad", "blade", "skive", "skiver", "glas", "pakke", "pakker",
        "håndfuld", "næve", "bæger", "bakke", "flaske",
    };

    private static readonly Regex IngredientAmountRx = new(
        @"^(?<amount>\d+[.,]?\d*\s*[½⅓⅔¼¾⅕⅖⅗⅘⅙⅚⅛⅜⅝⅞]|\d+\s*/\s*\d+|\d+\s*-\s*\d+|\d+[.,]\d+|\d+|[½⅓⅔¼¾⅕⅖⅗⅘⅙⅚⅛⅜⅝⅞])\s*(?<rest>.*)$",
        RegexOptions.Compiled);

    // "300 g hakket kød" → (300, g, hakket kød); "½ rødløg" → (½, "", rødløg); "1 æble i tern" → (1, "", æble i tern)
    private static ScannedIngredient ParseIngredientLine(string raw)
    {
        var m = IngredientAmountRx.Match(raw);
        if (!m.Success)
            return new ScannedIngredient("", "", raw);

        var amount = m.Groups["amount"].Value.Trim();
        var rest = m.Groups["rest"].Value.Trim();
        var spaceIdx = rest.IndexOf(' ');
        var unit = spaceIdx > 0 ? rest[..spaceIdx].TrimEnd('.') : "";
        if (KnownUnits.Contains(unit))
            return new ScannedIngredient(amount, unit, rest[(spaceIdx + 1)..].Trim());
        return new ScannedIngredient(amount, "", rest);
    }

    internal static decimal? TryParseAmount(string amount)
    {
        amount = amount.Trim();
        if (amount.Length == 0) return null;

        if (VulgarFractions.TryGetValue(amount[^1], out var frac))
        {
            var whole = amount[..^1].Trim();
            if (whole.Length == 0) return frac;
            return decimal.TryParse(whole, NumberStyles.Number, CultureInfo.InvariantCulture, out var w) ? w + frac : null;
        }

        var slash = amount.IndexOf('/');
        if (slash > 0)
        {
            return decimal.TryParse(amount[..slash].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var num) &&
                   decimal.TryParse(amount[(slash + 1)..].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var den) && den != 0
                ? num / den
                : null;
        }

        return decimal.TryParse(amount.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    // Del-opskrifter (fars + sovs + tilbehør) genbruger ofte samme ingrediens. Samme enhed+navn lægges
    // sammen (1 dl mælk + 2 dl mælk → 3 dl mælk); kan mængderne ikke lægges sammen (fx "2-3" eller
    // tomme "efter smag"-linjer), fjernes kun linjer der er helt ens.
    private static List<ScannedIngredient> MergeDuplicateIngredients(List<ScannedIngredient> items)
    {
        var result = new List<ScannedIngredient>();
        foreach (var group in items.GroupBy(i => (Unit: i.Unit.ToLowerInvariant(), Name: i.Name.ToLowerInvariant())))
        {
            var list = group.ToList();
            var amounts = list.Select(i => TryParseAmount(i.Amount)).ToList();
            if (list.Count > 1 && amounts.All(a => a.HasValue))
            {
                var sum = amounts.Sum(a => a!.Value);
                result.Add(list[0] with { Amount = sum.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',') });
            }
            else
            {
                result.AddRange(list.DistinctBy(i => (i.Amount, i.Unit.ToLowerInvariant(), i.Name.ToLowerInvariant())));
            }
        }
        return result;
    }

    // ── JSON-hjælpere ───────────────────────────────────────────────────────

    private static readonly Regex BlockBreakRx = new(@"<br\s*/?>|</p>|</li>", HtmlRx);

    private static string HtmlToText(string s)
    {
        s = WebUtility.HtmlDecode(s);
        s = BlockBreakRx.Replace(s, "\n");
        s = AnyTagRx.Replace(s, " ");
        return string.Join('\n', s.Split('\n').Select(l => HorizontalSpaceRx.Replace(l, " ").Trim())).Trim();
    }

    private static string? GetString(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String && HtmlToText(p.GetString()!) is { Length: > 0 } s
            ? s
            : null;

    private static IEnumerable<string?> GetStrings(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var p)) return Array.Empty<string?>();
        return p.ValueKind == JsonValueKind.Array
            ? p.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => (string?)HtmlToText(x.GetString()!)).ToList()
            : new[] { GetString(el, key) };
    }

    private static List<string> GetStringArray(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Array
            ? p.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => HtmlToText(x.GetString()!)).ToList()
            : new List<string>();

    private static int? GetPositiveInt(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var n) && n > 0 ? n : null;
}
