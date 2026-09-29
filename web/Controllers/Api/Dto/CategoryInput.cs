using System.ComponentModel.DataAnnotations;

namespace web.Controllers.Api.Dto
{
    /// <summary>Data til at oprette eller redigere en kategori.</summary>
    public class CategoryInput
    {
        [Required(ErrorMessage = "Navn mangler")]
        [MaxLength(100, ErrorMessage = "Navnet må højst være 100 tegn")]
        public string Name { get; set; } = "";

        /// <summary>Ikonnavn (Material Symbols). Standard er "cookie".</summary>
        [MaxLength(50, ErrorMessage = "Ikonnavnet må højst være 50 tegn")]
        public string? Icon { get; set; }
    }
}
