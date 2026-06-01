using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;

namespace web.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class SettingsController : Controller
    {
        private readonly AppDbContext _db;

        public SettingsController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            var counts = await _db.Recipes
                .GroupBy(r => r.Category)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var cat in categories)
                cat.RecipeCount = counts.FirstOrDefault(c => c.Name == cat.Name)?.Count ?? 0;

            return View(categories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(int id, string name, string icon)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null) return NotFound();

            cat.Name = name?.Trim() ?? cat.Name;
            cat.Icon = icon?.Trim() ?? cat.Icon;
            await _db.SaveChangesAsync();

            return Json(new { success = true, name = cat.Name, icon = cat.Icon });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(string name, string icon)
        {
            var cat = new Category
            {
                Name = name?.Trim() ?? "",
                Icon = icon?.Trim() is { Length: > 0 } i ? i : "cookie"
            };

            if (string.IsNullOrWhiteSpace(cat.Name))
                return BadRequest();

            _db.Categories.Add(cat);
            await _db.SaveChangesAsync();

            return Json(new { success = true, id = cat.Id, name = cat.Name, icon = cat.Icon });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null) return NotFound();

            // Nulstil kategoridata på alle tilknyttede opskrifter
            var affected = await _db.Recipes
                .Where(r => r.Category == cat.Name)
                .ToListAsync();

            foreach (var recipe in affected)
            {
                recipe.Category = "";
                recipe.CategoryIcon = "";
            }

            _db.Categories.Remove(cat);
            await _db.SaveChangesAsync();

            return Json(new { success = true, id });
        }
    }
}
