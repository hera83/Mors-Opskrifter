using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace web.Services.Api
{
    /// <summary>
    /// Sætter titel/beskrivelse på OpenAPI-dokumentet og erklærer X-Api-Key som sikkerhedsskema,
    /// så Swagger UI viser en "Authorize"-knap hvor nøglen kan indtastes.
    /// </summary>
    public class ApiDocumentTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Info = new OpenApiInfo
            {
                Title       = "Mors Opskrifter API",
                Version     = "v1",
                Description = "Læs og administrér opskrifter, ingredienser, fremgangsmåder og kategorier. " +
                              $"Alle kald kræver headeren `{ApiKeyAuthenticationHandler.HeaderName}` med den delte nøgle fra appsettings (Api:SharedKey).",
            };

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[ApiKeyAuthenticationHandler.SchemeName] = new OpenApiSecurityScheme
            {
                Type        = SecuritySchemeType.ApiKey,
                In          = ParameterLocation.Header,
                Name        = ApiKeyAuthenticationHandler.HeaderName,
                Description = "Delt API-nøgle (Api:SharedKey i appsettings).",
            };

            document.Security ??= new List<OpenApiSecurityRequirement>();
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(ApiKeyAuthenticationHandler.SchemeName, document)] = new List<string>(),
            });

            return Task.CompletedTask;
        }
    }
}
