using System.Linq;

namespace web.Models
{
    public class Recipe
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        public string CategoryIcon { get; set; } = "cookie";
        public int PrepTimeMinutes { get; set; }
        public int CookTimeMinutes { get; set; }
        public int Servings { get; set; }
        public Difficulty Difficulty { get; set; } = Difficulty.Middel;
        public string Author { get; set; } = "Mor";
        public DateTime LastModified { get; set; } = DateTime.Now;
        public List<Ingredient> Ingredients { get; set; } = new();
        public List<RecipeStep> RecipeSteps { get; set; } = new();
        public string Notes { get; set; } = "";
        public string? OriginalImagePath { get; set; }

        // Convenience property (not mapped) for views that use List<string>
        public List<string> Steps => RecipeSteps.OrderBy(s => s.SortOrder).Select(s => s.Text).ToList();
    }

    public static class RecipeExtensions
    {
        public static string TotalTimeLabel(this Recipe r)
        {
            int total = r.PrepTimeMinutes + r.CookTimeMinutes;
            if (total >= 60)
            {
                int h = total / 60;
                int m = total % 60;
                return m == 0 ? $"{h} t." : $"{h} t. {m} min";
            }
            return $"{total} min";
        }
    }
}

