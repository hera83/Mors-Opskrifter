using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace web.Services.Api
{
    /// <summary>
    /// Godkender API-kald ud fra headeren X-Api-Key, sammenlignet med Api:SharedKey i appsettings.
    /// Bruges kun af controllerne under /api — resten af sitet kører stadig på Identity-cookies.
    /// </summary>
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "ApiKey";
        public const string HeaderName = "X-Api-Key";

        private readonly IOptionsMonitor<ApiSettings> _settings;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IOptionsMonitor<ApiSettings> settings)
            : base(options, logger, encoder)
        {
            _settings = settings;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var provided = Request.Headers[HeaderName].ToString();
            if (string.IsNullOrEmpty(provided))
                return Task.FromResult(AuthenticateResult.NoResult());

            var expected = _settings.CurrentValue.SharedKey;
            if (string.IsNullOrWhiteSpace(expected))
            {
                Logger.LogWarning("API-kald afvist: Api:SharedKey er ikke sat i konfigurationen.");
                return Task.FromResult(AuthenticateResult.Fail("API-nøglen er ikke konfigureret på serveren."));
            }

            // Sammenlign hashes i konstant tid, så svartiden ikke afslører noget om nøglen.
            var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
            var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
            if (!CryptographicOperations.FixedTimeEquals(providedHash, expectedHash))
                return Task.FromResult(AuthenticateResult.Fail("Ugyldig API-nøgle."));

            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "api-klient") }, SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title  = "Manglende eller ugyldig API-nøgle",
                Detail = $"Send den delte nøgle i headeren {HeaderName}.",
            }, options: null, contentType: "application/problem+json");
        }
    }
}
