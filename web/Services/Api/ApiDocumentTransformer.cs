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

            // Gør server-adresserne relative ("/" eller "/<pathbase>"), så Swagger UI kalder API'et på
            // samme scheme og host som siden er åbnet på — også bag en reverse proxy med HTTPS.
            foreach (var server in document.Servers ?? [])
            {
                if (Uri.TryCreate(server.Url, UriKind.Absolute, out var uri))
                    server.Url = uri.AbsolutePath.TrimEnd('/') is { Length: > 0 } path ? path : "/";
            }

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
