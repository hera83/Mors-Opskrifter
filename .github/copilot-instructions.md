# Copilot Instructions

## Project Guidelines
- User does not want to use ViewData["Title"] in this project and wants future pages/features implemented without that pattern.
- Place partial views and all view files under the folder matching their controller/feature (e.g., Recipes/_RecipeList.cshtml), not in Views/Shared unless it is truly shared across multiple unrelated features. Use explicit paths (~/Views/...) when referencing partials outside the current controller folder.
- All database models must have one file per class, and the filename must match the table name (e.g., Recipe.cs, Ingredient.cs). This applies to all EF entities in the project.

## File Management
- Database files are stored in a folder called App_dbs (under the project's ContentRootPath).
- Uploaded files are stored in App_files (under ContentRootPath).
- File metadata (path, filename, size, etc.) is stored in the database, while the actual file is saved on disk under App_files.