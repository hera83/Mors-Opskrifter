using web.Models;

namespace web.Controllers.Api.Dto
{
    /// <summary>Opslagsværdier til filtre og formularer i klienter.</summary>
    public class LookupsDto
    {
        public List<Difficulty> Difficulties { get; set; } = new();
        public List<CategoryDto> Categories { get; set; } = new();

        /// <summary>Alle enheder brugt i opskrifterne (fx "dl", "g", "stk").</summary>
        public List<string> Units { get; set; } = new();

        /// <summary>Alle ingredienser brugt i opskrifterne, med antal opskrifter der bruger dem.</summary>
        public List<IngredientUsageDto> Ingredients { get; set; } = new();
    }
}
