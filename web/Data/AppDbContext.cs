using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using web.Models;

namespace web.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Recipe> Recipes => Set<Recipe>();
        public DbSet<Ingredient> Ingredients => Set<Ingredient>();
        public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<UserFavorite> UserFavorites => Set<UserFavorite>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Recipe>(e =>
            {
                e.HasKey(r => r.Id);
                e.Property(r => r.Title).IsRequired().HasMaxLength(200);
                e.Property(r => r.Category).HasMaxLength(100);
                e.Property(r => r.CategoryIcon).HasMaxLength(50);
                e.Property(r => r.Author).HasMaxLength(100);
                e.Property(r => r.Difficulty).HasConversion<string>();

                e.HasMany(r => r.Ingredients)
                 .WithOne()
                 .HasForeignKey(i => i.RecipeId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasMany(r => r.RecipeSteps)
                 .WithOne()
                 .HasForeignKey(s => s.RecipeId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Ingredient>(e =>
            {
                e.HasKey(i => i.Id);
                e.Property(i => i.Amount).HasMaxLength(50);
                e.Property(i => i.Unit).HasMaxLength(50);
                e.Property(i => i.Name).HasMaxLength(200);
            });

            builder.Entity<RecipeStep>(e =>
            {
                e.HasKey(s => s.Id);
                e.Property(s => s.Text).IsRequired();
            });

            builder.Entity<Category>(e =>
            {
                e.HasKey(c => c.Id);
                e.Property(c => c.Name).IsRequired().HasMaxLength(100);
                e.Property(c => c.Icon).HasMaxLength(50);
            });

            builder.Entity<UserFavorite>(e =>
            {
                e.HasKey(f => new { f.UserId, f.RecipeId });
                e.HasOne(f => f.User).WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(f => f.Recipe).WithMany().HasForeignKey(f => f.RecipeId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
