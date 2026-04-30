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
    IConfiguration configuration)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKey = configuration["Auth:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            // No key configured – allow all (useful in Testing/Development)
            var anonPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "anonymous")],
                Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(anonPrincipal, Scheme.Name)));
        }

        if (!Request.Headers.TryGetValue(Options.HeaderName, out var extractedKey) || extractedKey != configuredKey)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid or missing API key"));
        }

        var claims = new[] { new Claim(ClaimTypes.Name, "api-client") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
