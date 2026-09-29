using System.ComponentModel.DataAnnotations;
using web.Models;

namespace web.Controllers.Api.Dto
{
    /// <summary>Data til at oprette eller erstatte en opskrift.</summary>
    public class RecipeInput
    {
        [Required(ErrorMessage = "Titel mangler")]
        [MaxLength(200, ErrorMessage = "Titlen må højst være 200 tegn")]
        public string Title { get; set; } = "";

        /// <summary>Id på en eksisterende kategori. Har forrang for <see cref="Category"/>.</summary>
        public int? CategoryId { get; set; }

        /// <summary>Kategorinavn. Findes kategorien ikke, oprettes den automatisk.</summary>
        [MaxLength(100, ErrorMessage = "Kategorinavnet må højst være 100 tegn")]
        public string? Category { get; set; }

        /// <summary>Ikon til en ny kategori. Ignoreres hvis kategorien findes i forvejen (så bruges dens ikon).</summary>
        [MaxLength(50, ErrorMessage = "Ikonnavnet må højst være 50 tegn")]
        public string? CategoryIcon { get; set; }

        [Range(0, 100000, ErrorMessage = "Forberedelsestiden skal være 0 eller mere")]
        public int PrepTimeMinutes { get; set; }

        [Range(0, 100000, ErrorMessage = "Tilberedningstiden skal være 0 eller mere")]
        public int CookTimeMinutes { get; set; }

        [Range(0, 1000, ErrorMessage = "Antal personer skal være mellem 0 og 1000")]
        public int Servings { get; set; }

        public Difficulty Difficulty { get; set; } = Difficulty.Middel;

        /// <summary>Forfatter. Udelades den, bruges "Mor" ved oprettelse og den eksisterende ved redigering.</summary>
        [MaxLength(100, ErrorMessage = "Forfatteren må højst være 100 tegn")]
        public string? Author { get; set; }

        public string? Notes { get; set; }

        public List<IngredientInput> Ingredients { get; set; } = new();

        /// <summary>Fremgangsmådens trin i rækkefølge. Tomme trin springes over.</summary>
        public List<string> Steps { get; set; } = new();
    }
}
