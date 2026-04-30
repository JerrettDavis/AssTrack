using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace AssTrack.Api.Auth;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public string HeaderName { get; set; } = "X-Api-Key";
}

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration,
    IWebHostEnvironment environment)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKey = configuration["Auth:ApiKey"];
        var ingestKey = configuration["Auth:IngestApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            var isDevOrTesting = environment.IsDevelopment() ||
                string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);

            if (!isDevOrTesting)
            {
                Logger.LogCritical("Auth:ApiKey is not configured. All authenticated API requests will be rejected.");
                return Task.FromResult(AuthenticateResult.Fail("API key is not configured"));
            }

            // No key configured in Development/Testing – allow all with both roles
            var anonClaims = new[]
            {
                new Claim(ClaimTypes.Name, "anonymous"),
                new Claim(ClaimTypes.Role, "operator"),
                new Claim(ClaimTypes.Role, "ingest")
            };
            var anonPrincipal = new ClaimsPrincipal(new ClaimsIdentity(anonClaims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(anonPrincipal, Scheme.Name)));
        }

        Request.Headers.TryGetValue(Options.HeaderName, out var headerKey);
        var providedKey = headerKey.ToString();

        if (providedKey == configuredKey)
        {
            // Operator key: both roles
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "api-client"),
                new Claim(ClaimTypes.Role, "operator"),
                new Claim(ClaimTypes.Role, "ingest")
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }

        if (!string.IsNullOrWhiteSpace(ingestKey) && providedKey == ingestKey)
        {
            // Ingest-only key: ingest role only
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "api-client"),
                new Claim(ClaimTypes.Role, "ingest")
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid or missing API key"));
    }
}
