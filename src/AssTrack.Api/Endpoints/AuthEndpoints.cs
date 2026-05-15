using System.Security.Claims;
using AssTrack.Api.Auth;

namespace AssTrack.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/me", (HttpContext ctx) =>
        {
            var roles = ctx.User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var tier = ctx.User.FindFirstValue(AssTrackClaimTypes.AccessTier) ?? AssTrackAccessTiers.Community;
            return Results.Ok(new IdentityDto(roles, tier, BuildCapabilities(roles, tier)));
        })
        .RequireAuthorization();
    }

    private static string[] BuildCapabilities(IReadOnlyCollection<string> roles, string tier)
    {
        var capabilities = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roles.Contains(AssTrackRoles.Viewer))
        {
            capabilities.Add("read");
        }

        if (roles.Contains(AssTrackRoles.Operator))
        {
            capabilities.Add("read");
            capabilities.Add("write");
            capabilities.Add("acknowledge-alerts");
            capabilities.Add("manage-operations");
        }

        if (roles.Contains(AssTrackRoles.Admin))
        {
            capabilities.Add("admin");
            capabilities.Add("maintenance");
            capabilities.Add("manage-access");
        }

        if (roles.Contains(AssTrackRoles.Ingest))
        {
            capabilities.Add("ingest");
        }

        if (string.Equals(tier, AssTrackAccessTiers.Enterprise, StringComparison.OrdinalIgnoreCase))
        {
            capabilities.Add("enterprise");
            capabilities.Add("rbac");
        }

        return capabilities.ToArray();
    }

    private record IdentityDto(string[] Roles, string AccessTier, string[] Capabilities);
}
