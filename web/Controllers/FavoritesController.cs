using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Data;
using web.Models;

namespace web.Controllers
{
    [Authorize]
    public class FavoritesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public FavoritesController(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User) ?? "";
            var favorites = await _db.UserFavorites
                .Where(f => f.UserId == userId)
                .Select(f => f.Recipe)
                .OrderBy(r => r.Title)
                .ToListAsync();

            var favIds = new HashSet<int>(favorites.Select(r => r.Id));
            ViewBag.FavoriteIds = favIds;
            return View(favorites);
        }

        public async Task<IActionResult> Details(int id)
        {
            var recipe = await _db.Recipes
                .Include(r => r.Ingredients)
                .Include(r => r.RecipeSteps)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (recipe == null) return NotFound();

            var userId = _userManager.GetUserId(User) ?? "";
            var favIds = new HashSet<int>(
                await _db.UserFavorites
                    .Where(f => f.UserId == userId)
                    .Select(f => f.RecipeId)
                    .ToListAsync());

            ViewBag.AllRecipes = await _db.UserFavorites
                .Where(f => f.UserId == userId)
                .Select(f => f.Recipe)
                .OrderBy(r => r.Title)
                .ToListAsync();

            ViewBag.FavoriteIds = favIds;
            ViewBag.IsFavorite = favIds.Contains(id);
            return View(recipe);
        }
    }
}
