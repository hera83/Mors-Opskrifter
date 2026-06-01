namespace web.Models
{
    public class RecipeStep
    {
        public int Id { get; set; }
        public int RecipeId { get; set; }
        public int SortOrder { get; set; }
        public string Text { get; set; } = "";
    }
}
