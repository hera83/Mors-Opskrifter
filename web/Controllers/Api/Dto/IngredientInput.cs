using System.ComponentModel.DataAnnotations;

namespace web.Controllers.Api.Dto
{
    /// <summary>En ingredienslinje til oprettelse/redigering af en opskrift.</summary>
    public class IngredientInput
    {
        [MaxLength(50, ErrorMessage = "Mængden må højst være 50 tegn")]
        public string? Amount { get; set; }

        [MaxLength(50, ErrorMessage = "Enheden må højst være 50 tegn")]
        public string? Unit { get; set; }

        [Required(ErrorMessage = "Ingrediensnavn mangler")]
        [MaxLength(200, ErrorMessage = "Ingrediensnavnet må højst være 200 tegn")]
        public string Name { get; set; } = "";
    }
}
