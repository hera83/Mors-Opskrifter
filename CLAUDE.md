# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Mors Opskrifter ("Mom's Recipes") is a Danish-language ASP.NET Core MVC app (net10.0) for storing and browsing family recipes. UI text, comments, and validation messages throughout the codebase are in Danish — match that when adding new controller/view code and user-facing strings.

## Commands

All commands run from `web/` (the project directory containing `web.csproj`), unless noted otherwise.

```
dotnet build                          # build
dotnet run                            # run (see web/Properties/launchSettings.json for ports/profiles)
dotnet ef migrations add <Name>       # add an EF Core migration (needs dotnet-ef tool)
dotnet ef database update             # apply migrations
```

There is no automated test suite in this repo — verify changes by running the app and exercising the feature in the browser.

Docker: `docker-compose up --build` from the repo root builds `web/Dockerfile` and persists the SQLite DB and uploaded files in named volumes (`app_dbs`, `app_files`).

## Architecture

- **Standard ASP.NET Core MVC**, controller-per-feature under `web/Controllers/`, with server-rendered Razor views under `web/Views/<Controller>/`. There is no separate frontend build step — client JS/CSS lives directly in `web/wwwroot/js/site.js` and `web/wwwroot/css/site.css`.
- **Data layer**: `web/Data/AppDbContext.cs` is an `IdentityDbContext<ApplicationUser>` (SQLite, via `Microsoft.EntityFrameworkCore.Sqlite`). Entity configuration (keys, max lengths, cascade deletes) lives in `OnModelCreating` rather than separate `IEntityTypeConfiguration` classes. One model class per file in `web/Models/`, filename matching the table/entity name (e.g. `Recipe.cs`, `Ingredient.cs`) — follow this convention for any new entity.
- **Core domain**: `Recipe` has many `Ingredient` and `RecipeStep` (cascade delete), plus a free-text `Category`/`CategoryIcon` (categories are also tracked as their own `Category` entity for the settings UI, but a recipe's category is stored as a denormalized string, not a foreign key). `UserFavorite` is a join entity keyed on `(UserId, RecipeId)`.
- **Auth**: ASP.NET Core Identity with cookie auth (`web/Controllers/AuthController.cs`, `SetupController.cs`). There's no registration flow — `SetupController` runs a one-time first-run wizard that redirects to `/Auth/Login` once any user exists, and creates the first account as `Administrator`. Two roles exist: `Administrator` (can create/edit/delete recipes and categories) and `User`. Both roles are ensured at startup in `Program.cs`.
- **File storage** (see `web/Program.cs` and root-level `.github`-derived conventions):
  - SQLite DB file lives in `App_dbs/` under `ContentRootPath`.
  - Uploaded images live in `App_files/` under `ContentRootPath`, referenced from the DB only by filename (e.g. `Recipe.OriginalImagePath`); actual bytes are never stored in the DB. Served back out through `RecipesController.Image` (an anonymous-access action that resolves the filename against `App_files` and streams it with a content-type inferred from the extension).
- **Ollama integration** (`web/Services/Ollama/`): a typed client for a local/remote Ollama server, configured via the `Ollama` section in `appsettings.json` (`OllamaSettings.cs`, `OllamaConfigurationProvider`). `OllamaHttpClientFactory` builds the `HttpClient` (base URL, optional API key, timeout). `IOllamaService`/`OllamaService` wrap the Ollama HTTP API (chat, generate, embeddings, model management) using DTOs under `Services/Ollama/Dto/`. The server has an NVIDIA V100 with 16 GB VRAM — any model change must fit that (the chosen `gemma4:12b` uses ~8.4 GB at `num_ctx` 16384).
- **Recipe import / AI scan** (`web/Services/RecipeImport/RecipeImportService.cs`, called by `RecipesController.AiScanUrl`, consumed by `runScan`/`transferToForm` in `site.js`): fetches the URL, then (1) parses Schema.org `Recipe` JSON-LD if present — fast and exact, and preferred — and otherwise (2) sends a structure-preserving text extract of the page to `Ollama:DefaultChatModel` with a JSON-schema `format`. Both paths feed ingredient lines through the same `ParseIngredientLine` + `MergeDuplicateIngredients` (same unit+name is summed, e.g. 1 dl + 2 dl mælk → 3 dl). Gotchas: ASP.NET sites HTML-encode the script type (`application/ld&#x2B;json`); instructions often come as `HowToSection` → `itemListElement`; if the prompt exceeds `num_ctx`, Ollama silently drops about half of it — which is why only a window around the "Ingredienser" heading is sent. The JSON response shape (`ok`, `found`, `source`, `title`, `category`, `prepTime`, `cookTime`, `servings`, `difficulty`, `ingredients[{amount,unit,name}]`, `steps[]`) is a contract with `site.js`.
- **REST API** (`web/Controllers/Api/`, DTOs in `Controllers/Api/Dto/`): attribute-routed `[ApiController]`s under `/api/v1` (recipes, categories, lookups) for external consumers such as a meal-planning app. Auth is a separate `ApiKey` authentication scheme (`web/Services/Api/ApiKeyAuthenticationHandler.cs`) that checks the `X-Api-Key` header against `Api:SharedKey` — empty key means every API call is rejected; the MVC UI still uses Identity cookies. The OpenAPI document (`Microsoft.AspNetCore.OpenApi`, XML doc comments become descriptions) is at `/api/v1/openapi.json`, Swagger UI at `/api/v1/swagger`. The DTOs are a public contract — don't rename fields without versioning. In production, `/api` requests bypass the HTML error pages and get ProblemDetails JSON instead (see the `UseWhen` branches in `Program.cs`).
- **PDF export**: `RecipesController.DownloadPdf` renders a recipe to PDF using QuestPDF (community license, set in `Program.cs`).
- **Dev-only seeding**: `web/Data/DbSeeder.cs` inserts a fixed set of demo recipes/categories, but only runs when `app.Environment.IsDevelopment()` (wired up in `Program.cs`) and only if the `Recipes` table is empty — never runs in production regardless of DB state.
- **View conventions**: partial views live alongside the controller/feature folder that owns them (e.g. `Views/Recipes/_...cshtml`), not in `Views/Shared`, unless truly shared across unrelated features (e.g. `_Layout.cshtml`, `_ValidationScriptsPartial.cshtml`). When referencing a partial from outside its owning folder, use an explicit `~/Views/...` path. Views do not use `ViewData["Title"]` — pass title/heading data explicitly instead.
