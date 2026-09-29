using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Controllers.Api.Dto;
using web.Data;
using web.Models;
using web.Services.Api;

namespace web.Controllers.Api
{
    /// <summary>Kategorier som opskrifter grupperes i.</summary>
    [ApiController]
    [Route("api/v1/categories")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    [Tags("Kategorier")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class CategoriesApiController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CategoriesApiController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>Hent alle kategorier med antal opskrifter.</summary>
        [HttpGet]
        public async Task<ActionResult<List<CategoryDto>>> GetCategories()
            => await LoadCategoriesAsync(_db);

        /// <summary>Hent én kategori.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CategoryDto>> GetCategory(int id)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null) return NotFound();
            return await ToDtoAsync(cat);
        }

        /// <summary>Opret en kategori.</summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<CategoryDto>> CreateCategory(CategoryInput input)
        {
            var name = input.Name.Trim();
            if (await _db.Categories.AnyAsync(c => c.Name == name))
                return Problem(statusCode: StatusCodes.Status409Conflict, title: $"Kategorien \"{name}\" findes allerede");

            var cat = new Category
            {
                Name = name,
                Icon = string.IsNullOrWhiteSpace(input.Icon) ? "cookie" : input.Icon.Trim(),
            };
            _db.Categories.Add(cat);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCategory), new { id = cat.Id }, await ToDtoAsync(cat));
        }

        /// <summary>Omdøb en kategori eller skift dens ikon.</summary>
        /// <remarks>Ændringen slår igennem på alle opskrifter i kategorien. Udelades ikonet, bevares det nuværende.</remarks>
        [HttpPut("{id:int}")]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<CategoryDto>> UpdateCategory(int id, CategoryInput input)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null) return NotFound();

            var name = input.Name.Trim();
            if (await _db.Categories.AnyAsync(c => c.Id != id && c.Name == name))
                return Problem(statusCode: StatusCodes.Status409Conflict, title: $"Kategorien \"{name}\" findes allerede");

            var oldName = cat.Name;
            cat.Name = name;
            if (!string.IsNullOrWhiteSpace(input.Icon))
                cat.Icon = input.Icon.Trim();

            // Opskrifter gemmer kategorien som tekst, så navn og ikon skal følge med
            var recipes = await _db.Recipes.Where(r => r.Category == oldName).ToListAsync();
            foreach (var recipe in recipes)
            {
                recipe.Category     = cat.Name;
                recipe.CategoryIcon = cat.Icon;
            }

            await _db.SaveChangesAsync();
            return await ToDtoAsync(cat);
        }

        /// <summary>Slet en kategori.</summary>
        /// <remarks>Opskrifterne i kategorien bevares, men bliver ukategoriserede.</remarks>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null) return NotFound();

            // Samme opførsel som i indstillinger: nulstil kategoridata på tilknyttede opskrifter
            var recipes = await _db.Recipes.Where(r => r.Category == cat.Name).ToListAsync();
            foreach (var recipe in recipes)
            {
                recipe.Category     = "";
                recipe.CategoryIcon = "";
            }

            _db.Categories.Remove(cat);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        internal static async Task<List<CategoryDto>> LoadCategoriesAsync(AppDbContext db)
        {
            var categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();
            var counts = await db.Recipes
                .GroupBy(r => r.Category)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Name, g => g.Count);

            return categories
                .Select(c => new CategoryDto
                {
                    Id          = c.Id,
                    Name        = c.Name,
                    Icon        = c.Icon,
                    RecipeCount = counts.GetValueOrDefault(c.Name),
                })
                .ToList();
        }

        private async Task<CategoryDto> ToDtoAsync(Category cat) => new()
        {
            Id          = cat.Id,
            Name        = cat.Name,
            Icon        = cat.Icon,
            RecipeCount = await _db.Recipes.CountAsync(r => r.Category == cat.Name),
        };
    }
}
