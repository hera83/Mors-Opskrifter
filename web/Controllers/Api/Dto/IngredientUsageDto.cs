namespace web.Controllers.Api.Dto
{
    /// <summary>En ingrediens og hvor mange opskrifter den indgår i.</summary>
    public class IngredientUsageDto
    {
        public string Name { get; set; } = "";
        public int RecipeCount { get; set; }
    }
}
