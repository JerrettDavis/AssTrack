using System.Security.Claims;

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
                .ToArray();
            return Results.Ok(new IdentityDto(roles));
        })
        .RequireAuthorization();
    }

    private record IdentityDto(string[] Roles);
}
