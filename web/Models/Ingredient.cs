namespace web.Models
{
    public class Ingredient
    {
        public int Id { get; set; }
        public int RecipeId { get; set; }
        public string Amount { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Name { get; set; } = "";
    }
}
