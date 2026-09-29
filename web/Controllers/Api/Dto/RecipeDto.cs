using web.Models;

namespace web.Controllers.Api.Dto
{
    /// <summary>En opskrift med ingredienser og fremgangsmåde.</summary>
    public class RecipeDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";

        /// <summary>Id på kategorien, hvis opskriftens kategori findes i kategorilisten.</summary>
        public int? CategoryId { get; set; }

        /// <summary>Kategoriens navn (tom streng hvis opskriften ikke har en kategori).</summary>
        public string Category { get; set; } = "";

        /// <summary>Ikonnavn for kategorien (Material Symbols).</summary>
        public string CategoryIcon { get; set; } = "";

        public int PrepTimeMinutes { get; set; }
        public int CookTimeMinutes { get; set; }

        /// <summary>Forberedelse + tilberedning i minutter.</summary>
        public int TotalTimeMinutes { get; set; }

        /// <summary>Antal personer/portioner opskriften er beregnet til.</summary>
        public int Servings { get; set; }

        public Difficulty Difficulty { get; set; }
        public string Author { get; set; } = "";
        public string Notes { get; set; } = "";
        public DateTime LastModified { get; set; }

        /// <summary>Absolut URL til opskriftens billede (kræver ikke API-nøgle), eller null.</summary>
        public string? ImageUrl { get; set; }

        /// <summary>Ingredienser i den rækkefølge de står i opskriften.</summary>
        public List<IngredientDto> Ingredients { get; set; } = new();

        /// <summary>Fremgangsmådens trin i rækkefølge.</summary>
        public List<string> Steps { get; set; } = new();
    }
}
