using System.Net;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using web.Data;
using web.Models;
using web.Services.Ollama;
using web.Services.Ollama.Interfaces;
using web.Services.RecipeImport;

QuestPDF.Settings.License = LicenseType.Community;

// Gør ældre tegnsæt (fx windows-1252/ISO-8859-1) tilgængelige ved afkodning af
// hentede opskriftssider i RecipeImportService.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

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

// ── Ollama ───────────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("Ollama");
builder.Services.AddScoped<OllamaHttpClientFactory>();
builder.Services.AddScoped<IOllamaConfigurationProvider, OllamaConfigurationProvider>();
builder.Services.AddScoped<IOllamaService, OllamaService>();

// ── Opskrifts-import (AI-scan af URL) ────────────────────────────────────────
builder.Services.AddHttpClient(RecipeImportService.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("da,en;q=0.8");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect        = true,
    MaxAutomaticRedirections = 5,
    AutomaticDecompression   = DecompressionMethods.All,
});
builder.Services.AddScoped<RecipeImportService>();

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

