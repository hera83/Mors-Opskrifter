namespace web.Services.Api
{
    /// <summary>Konfiguration af REST-API'et (sektionen "Api" i appsettings).</summary>
    public class ApiSettings
    {
        public const string SectionName = "Api";

        /// <summary>
        /// Delt nøgle som klienter sender i headeren X-Api-Key. Er den tom, afvises alle API-kald.
        /// </summary>
        public string SharedKey { get; set; } = "";
    }
}
