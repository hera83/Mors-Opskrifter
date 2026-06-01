using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using web.Data;
using web.Models;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// ── Database ────────────────────────────────────────────────────────────────
var dbDir = Path.Combine(builder.Environment.ContentRootPath, "App_dbs");
Directory.CreateDirectory(dbDir);
var dbPath = Path.Combine(dbDir, "morsopskrifter.db");
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

// ── File storage ─────────────────────────────────────────────────────────────
var filesDir = Path.Combine(builder.Environment.ContentRootPath, "App_files");
Directory.CreateDirectory(filesDir);

// ── Identity ─────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(opt =>
{
    opt.Password.RequireDigit           = false;
    opt.Password.RequireLowercase       = false;
    opt.Password.RequireUppercase       = false;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequiredLength         = 6;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.LoginPath         = "/Auth/Login";
    opt.AccessDeniedPath  = "/Error/Forbidden";
    opt.SlidingExpiration = true;
    opt.ExpireTimeSpan    = TimeSpan.FromDays(30);
});

// ── MVC ──────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ── Migrate + Seed ───────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
        await DbSeeder.SeedRecipesAsync(db);

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Administrator", "User" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}

// ── Pipeline ─────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error/ServerError");
    app.UseStatusCodePagesWithReExecute("/Error/StatusCode/{0}");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Recipes}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

