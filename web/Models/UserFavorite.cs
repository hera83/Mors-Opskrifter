namespace web.Models
{
    public class UserFavorite
    {
        public string UserId { get; set; } = "";
        public int RecipeId { get; set; }

        public ApplicationUser User { get; set; } = null!;
        public Recipe Recipe { get; set; } = null!;
    }
}
