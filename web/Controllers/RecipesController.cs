using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using web.Data;
using web.Models;

namespace web.Controllers
{
    [Authorize]
    public class RecipesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;

        public RecipesController(AppDbContext db, IWebHostEnvironment env, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _env = env;
            _userManager = userManager;
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
            await _db.SaveChangesAsync();

            return Json(new { ok = true, id = recipe.Id });
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
}
