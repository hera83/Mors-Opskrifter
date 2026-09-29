namespace web.Controllers.Api.Dto
{
    /// <summary>En ingredienslinje, fx "2 dl mælk".</summary>
    public class IngredientDto
    {
        /// <summary>Mængden præcis som den står i opskriften (fri tekst, fx "2", "1½", "2-3").</summary>
        public string Amount { get; set; } = "";

        /// <summary>
        /// Mængden som tal, hvis den kan fortolkes ("1½" → 1.5, "1/2" → 0.5), ellers null (fx "2-3").
        /// Praktisk til at skalere portioner og lægge indkøbslister sammen.
        /// </summary>
        public decimal? Quantity { get; set; }

        public string Unit { get; set; } = "";
        public string Name { get; set; } = "";
    }
}
