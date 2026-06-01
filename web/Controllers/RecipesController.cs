using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using web.Data;
using web.Models;
using web.Services.Ollama;
using web.Services.Ollama.Dto;

namespace web.Controllers
{
    [Authorize]
    public class RecipesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOllamaService _ollama;
        private readonly IConfiguration _config;

        public RecipesController(AppDbContext db, IWebHostEnvironment env, UserManager<ApplicationUser> userManager,
            IOllamaService ollama, IConfiguration config)
        {
            _db = db;
            _env = env;
            _userManager = userManager;
            _ollama = ollama;
            _config = config;
        }

        private async Task<HashSet<int>> GetUserFavoriteIdsAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return new HashSet<int>();
            var ids = await _db.UserFavorites
                .Where(f => f.UserId == userId)
                .Select(f => f.RecipeId)
                .ToListAsync();
            return new HashSet<int>(ids);
        }

        public async Task<IActionResult> Index(string? search)
        {
            var query = _db.Recipes.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(r => r.Title.Contains(search) || r.Category.Contains(search));
            ViewBag.Search = search ?? "";
            ViewBag.FavoriteIds = await GetUserFavoriteIdsAsync();
            return View(await query.OrderBy(r => r.Title).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var recipe = await _db.Recipes
                .Include(r => r.Ingredients)
                .Include(r => r.RecipeSteps)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (recipe == null) return NotFound();
            ViewBag.AllRecipes = await _db.Recipes.OrderBy(r => r.Title).ToListAsync();
            var favIds = await GetUserFavoriteIdsAsync();
            ViewBag.FavoriteIds = favIds;
            ViewBag.IsFavorite = favIds.Contains(id);
            return View(recipe);
        }

        private async Task EnsureCategoryExistsAsync(string? categoryName, string? categoryIcon)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return;
            var name = categoryName.Trim();
            var exists = await _db.Categories.AnyAsync(c => c.Name == name);
            if (!exists)
            {
                _db.Categories.Add(new Category
                {
                    Name = name,
                    Icon = string.IsNullOrWhiteSpace(categoryIcon) ? "cookie" : categoryIcon.Trim()
                });
                await _db.SaveChangesAsync();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var cats = await _db.Categories
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, c.Icon })
                .ToListAsync();
            return Json(cats);
        }

        [HttpGet]
        public async Task<IActionResult> GetIngredientSuggestions()
        {
            var units = await _db.Set<Ingredient>()
                .Where(i => !string.IsNullOrEmpty(i.Unit))
                .Select(i => i.Unit)
                .Distinct()
                .OrderBy(u => u)
                .ToListAsync();

            var names = await _db.Set<Ingredient>()
                .Where(i => !string.IsNullOrEmpty(i.Name))
                .Select(i => i.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync();

            return Json(new { units, names });
        }

        [HttpGet]
        public async Task<IActionResult> GetRecipeData(int id)
        {
            var r = await _db.Recipes
                .Include(x => x.Ingredients)
                .Include(x => x.RecipeSteps)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (r == null) return NotFound();

            return Json(new
            {
                r.Id, r.Title, r.Category, r.CategoryIcon,
                r.PrepTimeMinutes, r.CookTimeMinutes, r.Servings,
                difficulty = r.Difficulty.ToString(),
                r.Notes,
                imagePath = r.OriginalImagePath != null
                    ? Url.Action("Image", "Recipes", new { fileName = r.OriginalImagePath })
                    : null,
                ingredients = r.Ingredients.Select(i => new { i.Amount, i.Unit, i.Name }),
                steps = r.RecipeSteps.OrderBy(s => s.SortOrder).Select(s => s.Text)
            });
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> UpdateRecipe(
            int id, string title, string category, string categoryIcon,
            int prepTimeMinutes, int cookTimeMinutes, int servings,
            Difficulty difficulty, string notes,
            IFormFile? image,
            [FromForm] List<string> ingAmount,
            [FromForm] List<string> ingUnit,
            [FromForm] List<string> ingName,
            [FromForm] List<string> stepText)
        {
            var recipe = await _db.Recipes
                .Include(r => r.Ingredients)
                .Include(r => r.RecipeSteps)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (recipe == null) return Json(new { ok = false, error = "Opskrift ikke fundet" });
            if (string.IsNullOrWhiteSpace(title)) return Json(new { ok = false, error = "Titel mangler" });

            if (image != null && image.Length > 0)
            {
                var filesDir = Path.Combine(_env.ContentRootPath, "App_files");
                Directory.CreateDirectory(filesDir);
                var ext = Path.GetExtension(image.FileName);
                var fileName = $"{Guid.NewGuid()}{ext}";
                using var stream = System.IO.File.Create(Path.Combine(filesDir, fileName));
                await image.CopyToAsync(stream);
                recipe.OriginalImagePath = fileName;
            }

            recipe.Title           = title.Trim();
            recipe.Category        = category ?? "";
            recipe.CategoryIcon    = categoryIcon ?? "cookie";
            recipe.PrepTimeMinutes = prepTimeMinutes;
            recipe.CookTimeMinutes = cookTimeMinutes;
            recipe.Servings        = servings;
            recipe.Difficulty      = difficulty;
            recipe.Notes           = notes ?? "";
            recipe.LastModified    = DateTime.Now;

            _db.RemoveRange(recipe.Ingredients);
            _db.RemoveRange(recipe.RecipeSteps);

            for (int i = 0; i < ingName.Count; i++)
            {
                var name = ingName[i]?.Trim() ?? "";
                if (string.IsNullOrEmpty(name)) continue;
                recipe.Ingredients.Add(new Ingredient
                {
                    Amount = ingAmount.ElementAtOrDefault(i) ?? "",
                    Unit   = ingUnit.ElementAtOrDefault(i) ?? "",
                    Name   = name,
                });
            }

            for (int i = 0; i < stepText.Count; i++)
            {
                var text = stepText[i]?.Trim() ?? "";
                if (string.IsNullOrEmpty(text)) continue;
                recipe.RecipeSteps.Add(new RecipeStep { SortOrder = i, Text = text });
            }

            await EnsureCategoryExistsAsync(category, categoryIcon);
            await _db.SaveChangesAsync();
            return Json(new { ok = true, id = recipe.Id });
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteRecipe(int id)
        {
            var recipe = await _db.Recipes
                .Include(r => r.Ingredients)
                .Include(r => r.RecipeSteps)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (recipe == null) return Json(new { ok = false, error = "Ikke fundet" });

            _db.RemoveRange(recipe.Ingredients);
            _db.RemoveRange(recipe.RecipeSteps);
            _db.Recipes.Remove(recipe);
            await _db.SaveChangesAsync();
            return Json(new { ok = true });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Image(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return NotFound();

            var path = Path.Combine(_env.ContentRootPath, "App_files", Path.GetFileName(fileName));
            if (!System.IO.File.Exists(path))
                return NotFound();

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var contentType = ext switch
            {
                ".webp" => "image/webp",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
            return PhysicalFile(path, contentType);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleFavorite(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var existing = await _db.UserFavorites
                .FirstOrDefaultAsync(f => f.UserId == userId && f.RecipeId == id);

            bool isFavorite;
            if (existing != null)
            {
                _db.UserFavorites.Remove(existing);
                isFavorite = false;
            }
            else
            {
                _db.UserFavorites.Add(new UserFavorite { UserId = userId, RecipeId = id });
                isFavorite = true;
            }
            await _db.SaveChangesAsync();
            return Json(new { ok = true, isFavorite });
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Create(
            string title, string category, string categoryIcon,
            int prepTimeMinutes, int cookTimeMinutes, int servings,
            Difficulty difficulty, string notes,
            IFormFile? image,
            [FromForm] List<string> ingAmount,
            [FromForm] List<string> ingUnit,
            [FromForm] List<string> ingName,
            [FromForm] List<string> stepText)
        {
            if (string.IsNullOrWhiteSpace(title))
                return Json(new { ok = false, error = "Titel mangler" });

            string? imagePath = null;
            if (image != null && image.Length > 0)
            {
                var filesDir = Path.Combine(_env.ContentRootPath, "App_files");
                Directory.CreateDirectory(filesDir);
                var ext = Path.GetExtension(image.FileName);
                var fileName = $"{Guid.NewGuid()}{ext}";
                using var stream = System.IO.File.Create(Path.Combine(filesDir, fileName));
                await image.CopyToAsync(stream);
                imagePath = fileName;
            }

            var user = await _userManager.GetUserAsync(User);
            var author = !string.IsNullOrWhiteSpace(user?.DisplayName) ? user.DisplayName
                       : !string.IsNullOrWhiteSpace(user?.UserName)    ? user.UserName
                       : "Mor";

            var recipe = new Recipe
            {
                Title          = title.Trim(),
                Category       = category ?? "",
                CategoryIcon   = categoryIcon ?? "cookie",
                PrepTimeMinutes = prepTimeMinutes,
                CookTimeMinutes = cookTimeMinutes,
                Servings       = servings,
                Difficulty     = difficulty,
                Author         = author,
                Notes          = notes ?? "",
                LastModified   = DateTime.Now,
                OriginalImagePath = imagePath,
            };

            for (int i = 0; i < ingName.Count; i++)
            {
                var name = ingName[i]?.Trim() ?? "";
                if (string.IsNullOrEmpty(name)) continue;
                recipe.Ingredients.Add(new Ingredient
                {
                    Amount = ingAmount.ElementAtOrDefault(i) ?? "",
                    Unit   = ingUnit.ElementAtOrDefault(i) ?? "",
                    Name   = name,
                });
            }

            for (int i = 0; i < stepText.Count; i++)
            {
                var text = stepText[i]?.Trim() ?? "";
                if (string.IsNullOrEmpty(text)) continue;
                recipe.RecipeSteps.Add(new RecipeStep { SortOrder = i, Text = text });
            }

            _db.Recipes.Add(recipe);
            await EnsureCategoryExistsAsync(category, categoryIcon);
            await _db.SaveChangesAsync();

            return Json(new { ok = true, id = recipe.Id });
        }

        [HttpPost]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> AiScanUrl([FromBody] AiScanRequest req, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(req?.Url))
                return Json(new { ok = false, error = "URL mangler" });

            if (!Uri.TryCreate(req.Url.Trim(), UriKind.Absolute, out var parsedUri) ||
                (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
                return Json(new { ok = false, error = "Ugyldig URL – skal starte med http:// eller https://" });

            string html;
            try
            {
                using var httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 });
                httpClient.Timeout = TimeSpan.FromSeconds(45);
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
                httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("da,en;q=0.8");
                html = await httpClient.GetStringAsync(parsedUri, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return Json(new { ok = false, error = "Siden tog for lang tid at svare (timeout efter 45 sek). Prøv igen." });
            }
            catch (HttpRequestException ex)
            {
                return Json(new { ok = false, error = $"Kunne ikke hente siden: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = $"Uventet fejl ved hentning af side: {ex.Message}" });
            }

            // ── Forsøg 1: JSON-LD structured data (Schema.org Recipe) ──────────
            var structuredResult = TryExtractJsonLdRecipe(html);
            if (structuredResult != null)
                return Json(structuredResult);

            // ── Forsøg 2: AI-baseret parsing ─────────────────────────────────────
            var pageText = CleanHtmlForAi(html);
            if (pageText.Length < 100)
                return Json(new { ok = false, error = "Siden returnerede ikke nok tekstindhold til analyse" });

            var model = _config["Ollama:DefaultChatModel"] ?? "gemma4:latest";

            var systemPrompt =
                "Du er en opskrifts-ekstraktor. Din opgave er at finde opskriftsdata fra sidetekst.\n" +
                "Svar UDELUKKENDE med et rent JSON-objekt – ingen forklaring, ingen markdown, ingen kodeblokke.\n" +
                "Format når opskrift er fundet:\n" +
                "{\"found\":true,\"title\":\"Boller i karry\",\"category\":\"Aftensmad\",\"prepTime\":15,\"cookTime\":30,\"servings\":4,\"difficulty\":\"Middel\"," +
                "\"ingredients\":[{\"amount\":\"2\",\"unit\":\"dl\",\"name\":\"mælk\"}],\"steps\":[\"Trin 1\",\"Trin 2\"]}\n" +
                "Format når ingen opskrift er fundet:\n" +
                "{\"found\":false}\n" +
                "Regler:\n" +
                "- title: opskriftens navn (eller null hvis ikke fundet)\n" +
                "- category: én kategori på dansk, fx Dessert, Aftensmad, Bagværk (eller null)\n" +
                "- prepTime: forberedelsestid i hele minutter som tal (eller null)\n" +
                "- cookTime: tilberedningstid i hele minutter som tal (eller null)\n" +
                "- servings: antal portioner som tal (eller null)\n" +
                "- difficulty: én af værdierne Let, Middel eller Svær (eller null)\n" +
                "- amount og unit kan være tomme strenge\n" +
                "- steps skal være hele sætninger\n" +
                "- Sæt null for felter du ikke kan finde – udelad dem ikke";

            var chatRequest = new OllamaChatRequest
            {
                Model  = model,
                Stream = false,
                Messages = new List<OllamaChatMessageDto>
                {
                    new OllamaChatMessageDto { Role = "system", Content = systemPrompt },
                    new OllamaChatMessageDto { Role = "user",   Content = "Ekstraher opskriften fra denne sidetekst:\n\n" + pageText }
                }
            };

            OllamaChatResponse chatResponse;
            try
            {
                chatResponse = await _ollama.ChatAsync(chatRequest, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return Json(new { ok = false, error = "Ollama svarede ikke i tide. Modellen er måske ved at vågne – prøv igen om et øjeblik." });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = $"Ollama fejl: {ex.Message}" });
            }

            var rawContent = chatResponse.Message?.Content ?? "";
            return ParseAiRecipeJson(rawContent);
        }

        // ── Hjælpemetode: udtræk JSON-LD Recipe structured data ──────────────────
        private static object? TryExtractJsonLdRecipe(string html)
        {
            var rx = new System.Text.RegularExpressions.Regex(
                @"<script[^>]+type=[""']application/ld\+json[""'][^>]*>(.*?)</script>",
                System.Text.RegularExpressions.RegexOptions.Singleline |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match m in rx.Matches(html))
            {
                var json = m.Groups[1].Value.Trim();
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var recipes = FindRecipeNodes(doc.RootElement);
                    foreach (var recipe in recipes)
                    {
                        var result = ExtractFromSchemaRecipe(recipe);
                        if (result != null) return result;
                    }
                }
                catch { /* malformed JSON-LD – prøv næste */ }
            }
            return null;
        }

        private static IEnumerable<JsonElement> FindRecipeNodes(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Object)
            {
                if (el.TryGetProperty("@type", out var typeProp))
                {
                    var typeVal = typeProp.ValueKind == JsonValueKind.Array
                        ? string.Join(",", typeProp.EnumerateArray().Select(e => e.GetString() ?? ""))
                        : typeProp.GetString() ?? "";
                    if (typeVal.Contains("Recipe", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return el;
                        yield break;
                    }
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

        private static object? ExtractFromSchemaRecipe(JsonElement recipe)
        {
            // ── Metadata ─────────────────────────────────────────────────────────
            static string? GetSchemaString(JsonElement el, string key)
            {
                if (!el.TryGetProperty(key, out var p)) return null;
                return p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            }

            var title    = GetSchemaString(recipe, "name")?.Trim();
            var category = GetSchemaString(recipe, "recipeCategory")?.Trim();

            // ISO 8601 duration: PT30M → 30, PT1H30M → 90
            static int? ParseIsoDuration(string? iso)
            {
                if (string.IsNullOrWhiteSpace(iso)) return null;
                var m = System.Text.RegularExpressions.Regex.Match(iso,
                    @"PT(?:(?<h>\d+)H)?(?:(?<m>\d+)M)?",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!m.Success) return null;
                int hours   = m.Groups["h"].Success ? int.Parse(m.Groups["h"].Value) : 0;
                int minutes = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value) : 0;
                int total   = hours * 60 + minutes;
                return total > 0 ? total : null;
            }

            var prepTime = ParseIsoDuration(GetSchemaString(recipe, "prepTime"));
            var cookTime = ParseIsoDuration(GetSchemaString(recipe, "cookTime"));

            int? servings = null;
            if (recipe.TryGetProperty("recipeYield", out var yieldEl))
            {
                var yieldStr = yieldEl.ValueKind == JsonValueKind.String  ? yieldEl.GetString()
                             : yieldEl.ValueKind == JsonValueKind.Array   ? yieldEl.EnumerateArray().FirstOrDefault().GetString()
                             : yieldEl.ValueKind == JsonValueKind.Number  ? yieldEl.GetRawText()
                             : null;
                if (yieldStr != null)
                {
                    var numMatch = System.Text.RegularExpressions.Regex.Match(yieldStr, @"\d+");
                    if (numMatch.Success && int.TryParse(numMatch.Value, out var yieldInt))
                        servings = yieldInt;
                }
            }

            // ── Ingredienser ─────────────────────────────────────────────────────
            var ingredients = new List<object>();
            if (recipe.TryGetProperty("recipeIngredient", out var ingsEl) && ingsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var ing in ingsEl.EnumerateArray())
                {
                    var raw = ing.GetString()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(raw)) continue;
                    var parts = raw.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                    string amount = "", unit = "", name = raw;
                    if (parts.Length >= 2 && decimal.TryParse(parts[0].Replace(',', '.'),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out _))
                    {
                        amount = parts[0];
                        if (parts.Length >= 3) { unit = parts[1]; name = string.Join(" ", parts[2..]); }
                        else                   { name = parts[1]; }
                    }
                    ingredients.Add(new { amount, unit, name });
                }
            }

            // ── Fremgangsmåde ─────────────────────────────────────────────────────
            var steps = new List<string>();
            if (recipe.TryGetProperty("recipeInstructions", out var stepsEl))
            {
                if (stepsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in stepsEl.EnumerateArray())
                    {
                        string? text = null;
                        if (s.ValueKind == JsonValueKind.String)
                            text = s.GetString();
                        else if (s.ValueKind == JsonValueKind.Object && s.TryGetProperty("text", out var t))
                            text = t.GetString();
                        if (!string.IsNullOrWhiteSpace(text)) steps.Add(text.Trim());
                    }
                }
                else if (stepsEl.ValueKind == JsonValueKind.String)
                {
                    var raw = stepsEl.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(raw)) steps.Add(raw.Trim());
                }
            }

            if (ingredients.Count == 0 && steps.Count == 0) return null;
            return new
            {
                ok = true, found = true, source = "structured",
                title, category, prepTime, cookTime, servings,
                difficulty = (string?)null,
                ingredients, steps
            };
        }

        // ── Hjælpemetode: rens HTML til AI-prompt ────────────────────────────────
        private static string CleanHtmlForAi(string html)
        {
            var opts = System.Text.RegularExpressions.RegexOptions.Singleline |
                       System.Text.RegularExpressions.RegexOptions.IgnoreCase;
            // Fjern script, style, nav, header, footer, aside blokke helt
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<(script|style|nav|header|footer|aside|noscript)[^>]*>.*?</\1>", " ", opts);
            // Fjern HTML-kommentarer
            html = System.Text.RegularExpressions.Regex.Replace(html, @"<!--.*?-->", " ", opts);
            // Fjern alle resterende tags
            html = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
            // Decode common HTML entities
            html = html.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
                       .Replace("&nbsp;", " ").Replace("&#160;", " ").Replace("&quot;", "\"")
                       .Replace("&#39;", "'").Replace("&apos;", "'");
            // Komprimer whitespace
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\s{2,}", " ").Trim();
            // Begræns til 14000 tegn – AI klarer sig bedst med fokuseret input
            return html.Length > 14000 ? html[..14000] : html;
        }

        // ── Hjælpemetode: udtræk JSON fra AI-svar (håndterer markdown mv.) ──────
        private static IActionResult ParseAiRecipeJson(string rawContent)
        {
            if (string.IsNullOrWhiteSpace(rawContent))
                return new JsonResult(new { ok = false, error = "Modellen returnerede et tomt svar" });

            // Fjern eventuelle markdown code fences: ```json ... ``` eller ``` ... ```
            var fenceRx = new System.Text.RegularExpressions.Regex(@"```(?:json)?\s*([\s\S]*?)\s*```");
            var fenceMatch = fenceRx.Match(rawContent);
            var candidate = fenceMatch.Success ? fenceMatch.Groups[1].Value : rawContent;

            // Find første '{' og sidste '}' for at isolere JSON
            var jsonStart = candidate.IndexOf('{');
            var jsonEnd   = candidate.LastIndexOf('}');
            if (jsonStart < 0 || jsonEnd < 0)
                return new JsonResult(new { ok = false, error = "Modellen svarede ikke i JSON-format. Prøv at scanne igen." });

            var jsonStr = candidate[jsonStart..(jsonEnd + 1)];
            try
            {
                using var doc = JsonDocument.Parse(jsonStr);
                var root  = doc.RootElement;

                // "found" kan være boolean eller string "true"/"false"
                bool found = false;
                if (root.TryGetProperty("found", out var foundProp))
                {
                    found = foundProp.ValueKind == JsonValueKind.True ||
                            (foundProp.ValueKind == JsonValueKind.String &&
                             foundProp.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true);
                }

                if (!found)
                    return new JsonResult(new { ok = true, found = false });

                var ingredients = new List<object>();
                if (root.TryGetProperty("ingredients", out var ingsEl) && ingsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ing in ingsEl.EnumerateArray())
                    {
                        string GetStr(JsonElement el, string key) =>
                            el.TryGetProperty(key, out var v) ? (v.GetString() ?? "") : "";
                        ingredients.Add(new
                        {
                            amount = GetStr(ing, "amount"),
                            unit   = GetStr(ing, "unit"),
                            name   = GetStr(ing, "name")
                        });
                    }
                }

                var steps = new List<string>();
                if (root.TryGetProperty("steps", out var stepsEl) && stepsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in stepsEl.EnumerateArray())
                    {
                        var text = s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(text)) steps.Add(text!.Trim());
                    }
                }

                if (ingredients.Count == 0 && steps.Count == 0)
                    return new JsonResult(new { ok = true, found = false });

                static string? GetOptStr(JsonElement r, string key) =>
                    r.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.Trim() : null;

                static int? GetOptInt(JsonElement r, string key)
                {
                    if (!r.TryGetProperty(key, out var v)) return null;
                    if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
                    if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var ns)) return ns;
                    return null;
                }

                var title      = GetOptStr(root, "title");
                var category   = GetOptStr(root, "category");
                var prepTime   = GetOptInt(root, "prepTime");
                var cookTime   = GetOptInt(root, "cookTime");
                var servings   = GetOptInt(root, "servings");
                var difficulty = GetOptStr(root, "difficulty");
                // Normaliser difficulty til kendte værdier
                difficulty = difficulty?.ToLower() switch
                {
                    "let" or "easy" or "nem"                    => "Let",
                    "svær" or "hard" or "difficult" or "svæ r" => "Svær",
                    "middel" or "medium" or "mellem"            => "Middel",
                    _                                           => null
                };

                return new JsonResult(new { ok = true, found = true, source = "ai",
                    title, category, prepTime, cookTime, servings, difficulty,
                    ingredients, steps });
            }
            catch (JsonException)
            {
                return new JsonResult(new { ok = false, error = "Kunne ikke fortolke AI-svaret som JSON. Prøv at scanne igen." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            var recipe = await _db.Recipes
                .Include(r => r.Ingredients)
                .Include(r => r.RecipeSteps)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (recipe == null) return NotFound();

            var steps = recipe.RecipeSteps.OrderBy(s => s.SortOrder).ToList();
            var culture = new System.Globalization.CultureInfo("da-DK");

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Content().Column(col =>
                    {
                        // Title
                        col.Item().Text(recipe.Title)
                            .FontSize(26).Bold().FontColor("#2d2d2d");

                        col.Item().PaddingBottom(4).Text(
                            string.IsNullOrWhiteSpace(recipe.Category) ? "Ukategoriseret" : recipe.Category)
                            .FontSize(13).FontColor("#888888");

                        // Meta row
                        col.Item().PaddingVertical(6).Row(row =>
                        {
                            row.AutoItem().Text($"Forfatter: {recipe.Author}").FontColor("#555555");
                            row.ConstantItem(20).Text(" ");
                            if (recipe.PrepTimeMinutes > 0 || recipe.CookTimeMinutes > 0)
                            {
                                int total = recipe.PrepTimeMinutes + recipe.CookTimeMinutes;
                                string timeLabel = total >= 60
                                    ? (total % 60 == 0 ? $"{total / 60} t." : $"{total / 60} t. {total % 60} min")
                                    : $"{total} min";
                                row.AutoItem().Text($"Tid: {timeLabel}").FontColor("#555555");
                                row.ConstantItem(20).Text(" ");
                            }
                            if (recipe.Servings > 0)
                                row.AutoItem().Text($"Portioner: {recipe.Servings}").FontColor("#555555");
                        });

                        col.Item().PaddingBottom(4).Text(
                            $"Sværhedsgrad: {recipe.Difficulty}  •  Sidst ændret: {recipe.LastModified.ToString("d. MMMM yyyy", culture)}")
                            .FontSize(10).FontColor("#aaaaaa");

                        // Image
                        if (!string.IsNullOrEmpty(recipe.OriginalImagePath))
                        {
                            var imgPath = Path.Combine(_env.ContentRootPath, "App_files", Path.GetFileName(recipe.OriginalImagePath));
                            if (System.IO.File.Exists(imgPath))
                            {
                                var imgBytes = System.IO.File.ReadAllBytes(imgPath);
                                col.Item().PaddingVertical(8)
                                    .MaxHeight(280)
                                    .Image(imgBytes)
                                    .FitArea();
                            }
                        }

                        // Divider
                        col.Item().PaddingVertical(8).LineHorizontal(1).LineColor("#e0e0e0");

                        // Ingredients
                        if (recipe.Ingredients.Any())
                        {
                            col.Item().PaddingBottom(6).Text("Ingredienser").FontSize(14).Bold().FontColor("#2d2d2d");
                            foreach (var ing in recipe.Ingredients)
                            {
                                var amtUnit = $"{ing.Amount} {ing.Unit}".Trim();
                                var line = string.IsNullOrEmpty(amtUnit) ? ing.Name : $"{amtUnit}  {ing.Name}";
                                col.Item().Text($"• {line}").FontColor("#333333");
                            }
                            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor("#e0e0e0");
                        }

                        // Steps
                        if (steps.Any())
                        {
                            col.Item().PaddingBottom(6).Text("Fremgangsmåde").FontSize(14).Bold().FontColor("#2d2d2d");
                            for (int i = 0; i < steps.Count; i++)
                            {
                                col.Item().PaddingBottom(6).Row(row =>
                                {
                                    row.ConstantItem(22).Text($"{i + 1}.").Bold().FontColor("#555555");
                                    row.RelativeItem().Text(steps[i].Text).FontColor("#333333");
                                });
                            }
                        }

                        // Notes
                        if (!string.IsNullOrWhiteSpace(recipe.Notes))
                        {
                            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor("#e0e0e0");
                            col.Item().PaddingBottom(4).Text("Noter").FontSize(14).Bold().FontColor("#2d2d2d");
                            col.Item().Text(recipe.Notes).FontColor("#444444");
                        }
                    });
                });
            });

            var bytes = pdf.GeneratePdf();
            var fileName = $"{recipe.Title.Replace(" ", "_")}.pdf";
            return File(bytes, "application/pdf", fileName);
        }
    }

    public class AiScanRequest
    {
        public string? Url { get; set; }
    }
}
