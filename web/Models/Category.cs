using System.ComponentModel.DataAnnotations.Schema;

namespace web.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "cookie";

        [NotMapped]
        public int RecipeCount { get; set; }
    }
}
