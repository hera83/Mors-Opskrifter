// ── DbSeeder ────────────────────────────────────────────────────────────────
// Seed-funktionen herunder indsætter de originale demo-opskrifter i databasen.
// Den køres kun hvis der ikke allerede er opskrifter i databasen.
// For at fjerne seed-data igen: slet alle opskrifter via appen eller
// kommenter kaldet til DbSeeder.SeedRecipesAsync(...) ud i Program.cs.
// ────────────────────────────────────────────────────────────────────────────

using Microsoft.EntityFrameworkCore;
using web.Models;

namespace web.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRecipesAsync(AppDbContext db)
        {
            if (await db.Recipes.AnyAsync())
                return;

            // Seed kategorier
            if (!await db.Categories.AnyAsync())
            {
                db.Categories.AddRange(
                    new Category { Id = 1, Name = "Brød & Bagværk",    Icon = "bread"    },
                    new Category { Id = 2, Name = "Kager & Desserter", Icon = "cake"     },
                    new Category { Id = 3, Name = "Morgenmad",         Icon = "croissant" },
                    new Category { Id = 4, Name = "Hverdagsretter",    Icon = "cookie"   }
                );
                await db.SaveChangesAsync();
            }

            // Seed opskrifter
            var recipes = new List<Recipe>
            {
                new Recipe
                {
                    Title           = "Franskbrød",
                    Category        = "Brød & Bagværk",
                    CategoryIcon    = "bread",
                    PrepTimeMinutes = 30,
                    CookTimeMinutes = 120,
                    Servings        = 10,
                    Difficulty      = Difficulty.Middel,
                    Author          = "Mor",
                    LastModified    = new DateTime(2026, 5, 10),
                    Notes           = "Bagte ved 200 grader og det blev perfekt.",
                    Ingredients     = new()
                    {
                        new() { Amount = "500", Unit = "g",   Name = "hvedemel"     },
                        new() { Amount = "25",  Unit = "g",   Name = "gær"          },
                        new() { Amount = "3",   Unit = "dl",  Name = "lunkent vand" },
                        new() { Amount = "1",   Unit = "tsk", Name = "salt"         },
                        new() { Amount = "1",   Unit = "tsk", Name = "sukker"       },
                    },
                    RecipeSteps = new()
                    {
                        new() { SortOrder = 1, Text = "Opløs gæren i lunkent vand med sukker." },
                        new() { SortOrder = 2, Text = "Bland mel og salt, tilsæt gærblandingen." },
                        new() { SortOrder = 3, Text = "Ælt dejen godt igennem i 10 minutter." },
                        new() { SortOrder = 4, Text = "Lad hæve tildækket i 1 time." },
                        new() { SortOrder = 5, Text = "Form til en aflang brødform og læg i smurt form." },
                        new() { SortOrder = 6, Text = "Bag ved 200°C i 30-35 minutter til gyldenbrun." },
                    },
                },
                new Recipe
                {
                    Title           = "Kanelsnegle",
                    Category        = "Brød & Bagværk",
                    CategoryIcon    = "croissant",
                    PrepTimeMinutes = 45,
                    CookTimeMinutes = 60,
                    Servings        = 16,
                    Difficulty      = Difficulty.Middel,
                    Author          = "Mor",
                    LastModified    = new DateTime(2026, 5, 12),
                    Notes           = "Dejen må ALDRIG blive for varm. Så bliver den doven som en mandag morgen. 😄",
                    Ingredients     = new()
                    {
                        new() { Amount = "500",  Unit = "g",   Name = "hvedemel"    },
                        new() { Amount = "50",   Unit = "g",   Name = "gær"         },
                        new() { Amount = "2½",   Unit = "dl",  Name = "mælk"        },
                        new() { Amount = "75",   Unit = "g",   Name = "sukker"      },
                        new() { Amount = "1",    Unit = "tsk", Name = "kardemomme"  },
                        new() { Amount = "75",   Unit = "g",   Name = "smør"        },
                        new() { Amount = "1",    Unit = "æg",  Name = ""            },
                    },
                    RecipeSteps = new()
                    {
                        new() { SortOrder = 1, Text = "Opløs gæren i lun mælk." },
                        new() { SortOrder = 2, Text = "Tilsæt sukker, kardemomme, æg og det bløde smør." },
                        new() { SortOrder = 3, Text = "Tilsæt melet lidt ad gangen og ælt dejen godt igennem." },
                        new() { SortOrder = 4, Text = "Lad dejen hæve tildækket i 45 minutter." },
                        new() { SortOrder = 5, Text = "Rul dejen ud til en stor firkant." },
                        new() { SortOrder = 6, Text = "Smør med blødt smør, drys med sukker og kanel." },
                        new() { SortOrder = 7, Text = "Rul sammen og skær i skiver." },
                        new() { SortOrder = 8, Text = "Efterhæv i 30 minutter." },
                        new() { SortOrder = 9, Text = "Bag ved 200°C varmluft i 12-15 minutter." },
                    },
                },
                new Recipe
                {
                    Title           = "Drømmekage",
                    Category        = "Kager & Desserter",
                    CategoryIcon    = "cake",
                    PrepTimeMinutes = 20,
                    CookTimeMinutes = 40,
                    Servings        = 12,
                    Difficulty      = Difficulty.Let,
                    Author          = "Mor",
                    LastModified    = new DateTime(2026, 4, 22),
                    Notes           = "Toppingen skal boble lidt på overfladen når den er klar.",
                    Ingredients     = new()
                    {
                        new() { Amount = "3",   Unit = "stk", Name = "æg"           },
                        new() { Amount = "200", Unit = "g",   Name = "sukker"       },
                        new() { Amount = "200", Unit = "g",   Name = "hvedemel"     },
                        new() { Amount = "1",   Unit = "tsk", Name = "bagepulver"   },
                        new() { Amount = "1",   Unit = "dl",  Name = "mælk"         },
                        new() { Amount = "100", Unit = "g",   Name = "kokos"        },
                        new() { Amount = "125", Unit = "g",   Name = "smør"         },
                        new() { Amount = "200", Unit = "g",   Name = "brun farin"   },
                    },
                    RecipeSteps = new()
                    {
                        new() { SortOrder = 1, Text = "Pisk æg og sukker luftigt." },
                        new() { SortOrder = 2, Text = "Tilsæt mel og bagepulver." },
                        new() { SortOrder = 3, Text = "Tilsæt lun mælk og bland godt." },
                        new() { SortOrder = 4, Text = "Bag ved 180°C i 25 min." },
                        new() { SortOrder = 5, Text = "Smelt smør, tilsæt brun farin og kokos." },
                        new() { SortOrder = 6, Text = "Bred toppingen over den halvbagte kage." },
                        new() { SortOrder = 7, Text = "Bag videre i 10-15 min til toppingen er gyldenbrun." },
                    },
                },
                new Recipe
                {
                    Title           = "Vaniljekranse",
                    Category        = "Kager & Desserter",
                    CategoryIcon    = "cookie",
                    PrepTimeMinutes = 30,
                    CookTimeMinutes = 15,
                    Servings        = 40,
                    Difficulty      = Difficulty.Let,
                    Author          = "Mor",
                    LastModified    = new DateTime(2026, 3, 15),
                    Notes           = "Sprøjteposen skal være kold for at dejen ikke flyder.",
                    Ingredients     = new()
                    {
                        new() { Amount = "250", Unit = "g",   Name = "smør"              },
                        new() { Amount = "150", Unit = "g",   Name = "sukker"            },
                        new() { Amount = "1",   Unit = "stk", Name = "vaniljestang"      },
                        new() { Amount = "1",   Unit = "stk", Name = "æg"               },
                        new() { Amount = "375", Unit = "g",   Name = "hvedemel"         },
                        new() { Amount = "75",  Unit = "g",   Name = "mandler, malede"  },
                    },
                    RecipeSteps = new()
                    {
                        new() { SortOrder = 1, Text = "Bland blødt smør, sukker og vanilje." },
                        new() { SortOrder = 2, Text = "Tilsæt æg og bland godt." },
                        new() { SortOrder = 3, Text = "Tilsæt mel og malede mandler." },
                        new() { SortOrder = 4, Text = "Pres dejen gennem en sprøjtpose med stjernedysse." },
                        new() { SortOrder = 5, Text = "Form til kranse på bagepapir." },
                        new() { SortOrder = 6, Text = "Bag ved 200°C i 8-10 minutter til lysegyldne." },
                    },
                },
                new Recipe
                {
                    Title           = "Lagkagebund",
                    Category        = "Kager & Desserter",
                    CategoryIcon    = "cake",
                    PrepTimeMinutes = 15,
                    CookTimeMinutes = 20,
                    Servings        = 8,
                    Difficulty      = Difficulty.Let,
                    Author          = "Mor",
                    LastModified    = new DateTime(2026, 2, 8),
                    Notes           = "Afkøl altid bundene før de samles.",
                    Ingredients     = new()
                    {
                        new() { Amount = "4",   Unit = "stk", Name = "æg"         },
                        new() { Amount = "150", Unit = "g",   Name = "sukker"     },
                        new() { Amount = "150", Unit = "g",   Name = "hvedemel"   },
                        new() { Amount = "1",   Unit = "tsk", Name = "bagepulver" },
                    },
                    RecipeSteps = new()
                    {
                        new() { SortOrder = 1, Text = "Pisk æg og sukker meget luftigt – mindst 10 min." },
                        new() { SortOrder = 2, Text = "Sigt mel og bagepulver i og vend forsigtigt i æggemassen." },
                        new() { SortOrder = 3, Text = "Hæld i en smurt springform." },
                        new() { SortOrder = 4, Text = "Bag ved 175°C i 20-25 min." },
                        new() { SortOrder = 5, Text = "Afkøl på bagerist." },
                    },
                },
                new Recipe
                {
                    Title           = "Rugbrød",
                    Category        = "Brød & Bagværk",
                    CategoryIcon    = "bread",
                    PrepTimeMinutes = 20,
                    CookTimeMinutes = 70,
                    Servings        = 12,
                    Difficulty      = Difficulty.Svær,
                    Author          = "Mor",
                    LastModified    = new DateTime(2026, 1, 30),
                    Notes           = "Husk at starte surdej dagen i forvejen.",
                    Ingredients     = new()
                    {
                        new() { Amount = "500", Unit = "g",    Name = "rugmel"           },
                        new() { Amount = "200", Unit = "g",    Name = "hvedemel"         },
                        new() { Amount = "3",   Unit = "dl",   Name = "kærnemælk"        },
                        new() { Amount = "2",   Unit = "dl",   Name = "vand"             },
                        new() { Amount = "1",   Unit = "spsk", Name = "salt"             },
                        new() { Amount = "2",   Unit = "spsk", Name = "maltsirup"        },
                        new() { Amount = "1",   Unit = "pose", Name = "tørgær"           },
                        new() { Amount = "100", Unit = "g",    Name = "solsikkekerner"   },
                    },
                    RecipeSteps = new()
                    {
                        new() { SortOrder = 1, Text = "Bland mel, salt og gær i en stor skål." },
                        new() { SortOrder = 2, Text = "Tilsæt kærnemælk, vand og maltsirup." },
                        new() { SortOrder = 3, Text = "Tilsæt solsikkekerner og rør igen." },
                        new() { SortOrder = 4, Text = "Hæld i smurt rugbrødsform og glat overfladen." },
                        new() { SortOrder = 5, Text = "Lad hæve under et viskestykke i 1-2 timer." },
                        new() { SortOrder = 6, Text = "Bag ved 175°C i ca. 70 minutter." },
                        new() { SortOrder = 7, Text = "Afkøl på bagerist." },
                    },
                },
            };

            db.Recipes.AddRange(recipes);
            await db.SaveChangesAsync();
        }
    }
}
