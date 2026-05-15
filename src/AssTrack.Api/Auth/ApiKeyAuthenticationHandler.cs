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
        var adminKey = configuration["Auth:AdminApiKey"];
        var ingestKey = configuration["Auth:IngestApiKey"];
        var configuredTier = AssTrackAccessTiers.Normalize(configuration["Auth:AccessTier"]);

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            var isDevOrTesting = environment.IsDevelopment() ||
                string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase);

            if (!isDevOrTesting)
            {
                Logger.LogCritical("Auth:ApiKey is not configured. All authenticated API requests will be rejected.");
                return Task.FromResult(AuthenticateResult.Fail("API key is not configured"));
            }

            // No key configured in Development/Testing keeps local workflows fully enabled.
            var anonClaims = new[]
            {
                new Claim(ClaimTypes.Name, "anonymous"),
                new Claim(ClaimTypes.Role, AssTrackRoles.Viewer),
                new Claim(ClaimTypes.Role, AssTrackRoles.Operator),
                new Claim(ClaimTypes.Role, AssTrackRoles.Admin),
                new Claim(ClaimTypes.Role, AssTrackRoles.Ingest),
                new Claim(AssTrackClaimTypes.AccessTier, configuredTier)
            };
            var anonPrincipal = new ClaimsPrincipal(new ClaimsIdentity(anonClaims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(anonPrincipal, Scheme.Name)));
        }

        Request.Headers.TryGetValue(Options.HeaderName, out var headerKey);
        var providedKey = headerKey.ToString();

        if (!string.IsNullOrWhiteSpace(adminKey) && providedKey == adminKey)
        {
            var claims = BuildClaims("admin-api-client", configuredTier, AssTrackRoles.Admin, AssTrackRoles.Operator, AssTrackRoles.Viewer, AssTrackRoles.Ingest);
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }

        if (providedKey == configuredKey)
        {
            var roles = string.IsNullOrWhiteSpace(adminKey)
                ? [AssTrackRoles.Admin, AssTrackRoles.Operator, AssTrackRoles.Viewer, AssTrackRoles.Ingest]
                : new[] { AssTrackRoles.Operator, AssTrackRoles.Viewer, AssTrackRoles.Ingest };
            var claims = BuildClaims("api-client", configuredTier, roles);
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }

        if (!string.IsNullOrWhiteSpace(ingestKey) && providedKey == ingestKey)
        {
            var claims = BuildClaims("ingest-api-client", AssTrackAccessTiers.Device, AssTrackRoles.Ingest);
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid or missing API key"));
    }

    private static Claim[] BuildClaims(string name, string accessTier, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, name),
            new(AssTrackClaimTypes.AccessTier, accessTier)
        };

        claims.AddRange(roles.Distinct(StringComparer.OrdinalIgnoreCase).Select(role => new Claim(ClaimTypes.Role, role)));
        return claims.ToArray();
    }

}
