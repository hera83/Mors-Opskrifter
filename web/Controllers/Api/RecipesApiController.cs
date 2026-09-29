using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Controllers.Api.Dto;
using web.Data;
using web.Models;
using web.Services.Api;
using web.Services.RecipeImport;

namespace web.Controllers.Api
{
    /// <summary>Opskrifter med ingredienser, fremgangsmåde og billede.</summary>
    [ApiController]
    [Route("api/v1/recipes")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    [Tags("Opskrifter")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class RecipesApiController : ControllerBase
    {
        private const int MaxPageSize = 200;
        private const long MaxImageBytes = 10 * 1024 * 1024;
        private static readonly HashSet<string> AllowedImageExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public RecipesApiController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        /// <summary>Søg og filtrér i opskrifter.</summary>
        /// <remarks>Alle filtre kombineres (AND). Tekstsøgning er uden forskel på store/små bogstaver for a-z.</remarks>
        /// <param name="search">Fritekst i titel, kategori, noter eller ingrediensnavne.</param>
        /// <param name="category">Præcist kategorinavn.</param>
        /// <param name="categoryId">Kategori-id.</param>
        /// <param name="difficulty">Sværhedsgrad.</param>
        /// <param name="maxTotalMinutes">Maks. samlet tid (forberedelse + tilberedning) i minutter.</param>
        /// <param name="ingredient">Opskriften skal indeholde en ingrediens hvis navn indeholder teksten. Kan angives flere gange (alle skal matche).</param>
        /// <param name="modifiedSince">Kun opskrifter ændret på eller efter dette tidspunkt — praktisk til synkronisering.</param>
        /// <param name="sort">Sortering.</param>
        /// <param name="page">Sidenummer (starter ved 1).</param>
        /// <param name="pageSize">Antal pr. side (1–200).</param>
        [HttpGet]
        public async Task<ActionResult<PagedResult<RecipeDto>>> GetRecipes(
            [FromQuery] string? search,
            [FromQuery] string? category,
            [FromQuery] int? categoryId,
            [FromQuery] Difficulty? difficulty,
            [FromQuery] int? maxTotalMinutes,
            [FromQuery] List<string>? ingredient,
            [FromQuery] DateTime? modifiedSince,
            [FromQuery] RecipeSort sort = RecipeSort.Title,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            page     = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var query = _db.Recipes.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var p = LikePattern(search);
                query = query.Where(r =>
                    EF.Functions.Like(r.Title, p, "\\") ||
                    EF.Functions.Like(r.Category, p, "\\") ||
                    EF.Functions.Like(r.Notes, p, "\\") ||
                    r.Ingredients.Any(i => EF.Functions.Like(i.Name, p, "\\")));
            }

            if (categoryId is int catId)
            {
                var catName = await _db.Categories.Where(c => c.Id == catId).Select(c => c.Name).FirstOrDefaultAsync();
                if (catName == null)
                    return new PagedResult<RecipeDto> { Page = page, PageSize = pageSize };
                query = query.Where(r => r.Category == catName);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                var name = category.Trim();
                query = query.Where(r => r.Category == name);
            }

            if (difficulty is Difficulty d)
                query = query.Where(r => r.Difficulty == d);

            if (maxTotalMinutes is int max)
                query = query.Where(r => r.PrepTimeMinutes + r.CookTimeMinutes <= max);

            foreach (var ing in ingredient?.Where(s => !string.IsNullOrWhiteSpace(s)) ?? [])
            {
                var p = LikePattern(ing);
                query = query.Where(r => r.Ingredients.Any(i => EF.Functions.Like(i.Name, p, "\\")));
            }

            if (modifiedSince is DateTime since)
                query = query.Where(r => r.LastModified >= since);

            var total = await query.CountAsync();

            query = sort switch
            {
                RecipeSort.LastModified => query.OrderByDescending(r => r.LastModified).ThenBy(r => r.Id),
                RecipeSort.TotalTime    => query.OrderBy(r => r.PrepTimeMinutes + r.CookTimeMinutes).ThenBy(r => r.Title).ThenBy(r => r.Id),
                _                       => query.OrderBy(r => r.Title).ThenBy(r => r.Id),
            };

            var recipes = await query
                .Include(r => r.Ingredients)
                .Include(r => r.RecipeSteps)
                .AsSplitQuery()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var categoryIds = await GetCategoryIdsByNameAsync();
            return new PagedResult<RecipeDto>
            {
                Items      = recipes.Select(r => ToDto(r, categoryIds)).ToList(),
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            };
        }

        /// <summary>Hent én opskrift.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RecipeDto>> GetRecipe(int id)
        {
            var recipe = await LoadRecipeAsync(id);
            if (recipe == null) return NotFound();
            return ToDto(recipe, await GetCategoryIdsByNameAsync());
        }

        /// <summary>Opret en opskrift.</summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<RecipeDto>> CreateRecipe(RecipeInput input)
        {
            var cat = await ResolveCategoryAsync(input);
            if (cat == null) return ValidationProblem(ModelState);

            var recipe = new Recipe
            {
                Author = string.IsNullOrWhiteSpace(input.Author) ? "Mor" : input.Author.Trim(),
            };
            ApplyInput(recipe, input, cat.Value);

            _db.Recipes.Add(recipe);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRecipe), new { id = recipe.Id }, ToDto(recipe, await GetCategoryIdsByNameAsync()));
        }

        /// <summary>Erstat en opskrift.</summary>
        /// <remarks>Alle felter, ingredienser og trin overskrives med det sendte. Billedet bevares.</remarks>
        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RecipeDto>> UpdateRecipe(int id, RecipeInput input)
        {
            var recipe = await LoadRecipeAsync(id);
            if (recipe == null) return NotFound();

            var cat = await ResolveCategoryAsync(input);
            if (cat == null) return ValidationProblem(ModelState);

            if (!string.IsNullOrWhiteSpace(input.Author))
                recipe.Author = input.Author.Trim();

            _db.RemoveRange(recipe.Ingredients);
            _db.RemoveRange(recipe.RecipeSteps);
            recipe.Ingredients.Clear();
            recipe.RecipeSteps.Clear();
            ApplyInput(recipe, input, cat.Value);

            await _db.SaveChangesAsync();
            return ToDto(recipe, await GetCategoryIdsByNameAsync());
        }

        /// <summary>Slet en opskrift.</summary>
        /// <remarks>Sletter også ingredienser, trin, favoritmarkeringer og billedfilen.</remarks>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteRecipe(int id)
        {
            var recipe = await LoadRecipeAsync(id);
            if (recipe == null) return NotFound();

            var imageFile = recipe.OriginalImagePath;
            _db.RemoveRange(recipe.Ingredients);
            _db.RemoveRange(recipe.RecipeSteps);
            _db.Recipes.Remove(recipe);
            await _db.SaveChangesAsync();

            await DeleteImageFileIfUnusedAsync(imageFile);
            return NoContent();
        }

        /// <summary>Upload eller erstat opskriftens billede.</summary>
        /// <remarks>Tilladte formater: jpg, jpeg, png, webp, gif. Maks. 10 MB.</remarks>
        [HttpPut("{id:int}/image")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaxImageBytes + 64 * 1024)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RecipeDto>> UploadImage(int id, IFormFile file)
        {
            var recipe = await LoadRecipeAsync(id);
            if (recipe == null) return NotFound();

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (file.Length == 0)
                ModelState.AddModelError(nameof(file), "Filen er tom");
            else if (file.Length > MaxImageBytes)
                ModelState.AddModelError(nameof(file), "Billedet må højst være 10 MB");
            else if (!AllowedImageExtensions.Contains(ext))
                ModelState.AddModelError(nameof(file), "Kun jpg, jpeg, png, webp og gif er tilladt");
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var filesDir = Path.Combine(_env.ContentRootPath, "App_files");
            Directory.CreateDirectory(filesDir);
            var fileName = $"{Guid.NewGuid()}{ext}";
            await using (var stream = System.IO.File.Create(Path.Combine(filesDir, fileName)))
                await file.CopyToAsync(stream);

            var oldFile = recipe.OriginalImagePath;
            recipe.OriginalImagePath = fileName;
            recipe.LastModified      = DateTime.Now;
            await _db.SaveChangesAsync();

            await DeleteImageFileIfUnusedAsync(oldFile);
            return ToDto(recipe, await GetCategoryIdsByNameAsync());
        }

        /// <summary>Fjern opskriftens billede.</summary>
        [HttpDelete("{id:int}/image")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteImage(int id)
        {
            var recipe = await _db.Recipes.FindAsync(id);
            if (recipe == null) return NotFound();

            var oldFile = recipe.OriginalImagePath;
            recipe.OriginalImagePath = null;
            recipe.LastModified      = DateTime.Now;
            await _db.SaveChangesAsync();

            await DeleteImageFileIfUnusedAsync(oldFile);
            return NoContent();
        }

        // ── Hjælpere ────────────────────────────────────────────────────────────

        private Task<Recipe?> LoadRecipeAsync(int id) => _db.Recipes
            .Include(r => r.Ingredients)
            .Include(r => r.RecipeSteps)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id);

        // Opskriftens kategori er et denormaliseret navn; slå id'et op så klienter også kan filtrere på id.
        private async Task<Dictionary<string, int>> GetCategoryIdsByNameAsync()
        {
            var cats = await _db.Categories.Select(c => new { c.Name, c.Id }).ToListAsync();
            return cats.GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.Min(c => c.Id));
        }

        // Finder (eller opretter) kategorien ud fra CategoryId eller Category. Returnerer null og
        // tilføjer en ModelState-fejl, hvis et CategoryId ikke findes.
        private async Task<(string Name, string Icon)?> ResolveCategoryAsync(RecipeInput input)
        {
            if (input.CategoryId is int catId)
            {
                var byId = await _db.Categories.FindAsync(catId);
                if (byId == null)
                {
                    ModelState.AddModelError(nameof(RecipeInput.CategoryId), $"Kategori med id {catId} findes ikke");
                    return null;
                }
                return (byId.Name, byId.Icon);
            }

            var name = input.Category?.Trim();
            if (string.IsNullOrEmpty(name))
                return ("", "cookie");

            var existing = await _db.Categories.FirstOrDefaultAsync(c => c.Name == name);
            if (existing != null)
                return (existing.Name, existing.Icon);

            var icon = string.IsNullOrWhiteSpace(input.CategoryIcon) ? "cookie" : input.CategoryIcon.Trim();
            _db.Categories.Add(new Category { Name = name, Icon = icon });
            return (name, icon);
        }

        private static void ApplyInput(Recipe recipe, RecipeInput input, (string Name, string Icon) category)
        {
            recipe.Title           = input.Title.Trim();
            recipe.Category        = category.Name;
            recipe.CategoryIcon    = category.Icon;
            recipe.PrepTimeMinutes = input.PrepTimeMinutes;
            recipe.CookTimeMinutes = input.CookTimeMinutes;
            recipe.Servings        = input.Servings;
            recipe.Difficulty      = input.Difficulty;
            recipe.Notes           = input.Notes ?? "";
            recipe.LastModified    = DateTime.Now;

            foreach (var ing in input.Ingredients)
            {
                var name = ing.Name?.Trim() ?? "";
                if (string.IsNullOrEmpty(name)) continue;
                recipe.Ingredients.Add(new Ingredient
                {
                    Amount = ing.Amount?.Trim() ?? "",
                    Unit   = ing.Unit?.Trim() ?? "",
                    Name   = name,
                });
            }

            var sortOrder = 0;
            foreach (var step in input.Steps)
            {
                var text = step?.Trim() ?? "";
                if (string.IsNullOrEmpty(text)) continue;
                recipe.RecipeSteps.Add(new RecipeStep { SortOrder = sortOrder++, Text = text });
            }
        }

        private RecipeDto ToDto(Recipe r, Dictionary<string, int> categoryIds) => new()
        {
            Id               = r.Id,
            Title            = r.Title,
            CategoryId       = categoryIds.TryGetValue(r.Category, out var catId) ? catId : null,
            Category         = r.Category,
            CategoryIcon     = r.CategoryIcon,
            PrepTimeMinutes  = r.PrepTimeMinutes,
            CookTimeMinutes  = r.CookTimeMinutes,
            TotalTimeMinutes = r.PrepTimeMinutes + r.CookTimeMinutes,
            Servings         = r.Servings,
            Difficulty       = r.Difficulty,
            Author           = r.Author,
            Notes            = r.Notes,
            LastModified     = r.LastModified,
            ImageUrl         = r.OriginalImagePath != null
                ? Url.Action("Image", "Recipes", new { fileName = r.OriginalImagePath }, Request.Scheme)
                : null,
            Ingredients = r.Ingredients
                .OrderBy(i => i.Id)
                .Select(i => new IngredientDto
                {
                    Amount   = i.Amount,
                    Quantity = RecipeImportService.TryParseAmount(i.Amount),
                    Unit     = i.Unit,
                    Name     = i.Name,
                })
                .ToList(),
            Steps = r.RecipeSteps.OrderBy(s => s.SortOrder).Select(s => s.Text).ToList(),
        };

        private async Task DeleteImageFileIfUnusedAsync(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            if (await _db.Recipes.AnyAsync(r => r.OriginalImagePath == fileName)) return;

            var path = Path.Combine(_env.ContentRootPath, "App_files", Path.GetFileName(fileName));
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }

        private static string LikePattern(string text) =>
            "%" + text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
    }
}
