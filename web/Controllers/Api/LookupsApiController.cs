using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using web.Controllers.Api.Dto;
using web.Data;
using web.Models;
using web.Services.Api;

namespace web.Controllers.Api
{
    /// <summary>Opslagsværdier til filtre, søgning og formularer.</summary>
    [ApiController]
    [Route("api/v1/lookups")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
    [Tags("Opslag")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class LookupsApiController : ControllerBase
    {
        private readonly AppDbContext _db;

        public LookupsApiController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>Hent sværhedsgrader, kategorier, enheder og ingredienser i ét kald.</summary>
        [HttpGet]
        public async Task<ActionResult<LookupsDto>> GetLookups()
        {
            var units = await _db.Ingredients
                .Where(i => i.Unit != "")
                .Select(i => i.Unit)
                .Distinct()
                .OrderBy(u => u)
                .ToListAsync();

            var ingredients = await _db.Ingredients
                .Where(i => i.Name != "")
                .GroupBy(i => i.Name)
                .Select(g => new IngredientUsageDto
                {
                    Name        = g.Key,
                    RecipeCount = g.Select(i => i.RecipeId).Distinct().Count(),
                })
                .OrderBy(i => i.Name)
                .ToListAsync();

            return new LookupsDto
            {
                Difficulties = Enum.GetValues<Difficulty>().ToList(),
                Categories   = await CategoriesApiController.LoadCategoriesAsync(_db),
                Units        = units,
                Ingredients  = ingredients,
            };
        }
    }
}
