namespace web.Controllers.Api.Dto
{
    /// <summary>En kategori og hvor mange opskrifter der ligger i den.</summary>
    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        /// <summary>Ikonnavn (Material Symbols).</summary>
        public string Icon { get; set; } = "";

        public int RecipeCount { get; set; }
    }
}
