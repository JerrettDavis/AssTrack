using AssTrack.Api.Services;

namespace AssTrack.Api.Endpoints;

public static class SseTokenEndpoints
{
    public record SseTokenResponse(string Token, DateTimeOffset ExpiresAt);

    public static RouteGroupBuilder MapSseTokenEndpoints(this RouteGroupBuilder group)
    {
        // POST /api/events/token — authenticated (inherited from parent group)
        group.MapPost("/events/token", (ISseTokenService tokenService) =>
        {
            var (token, expiresAt) = tokenService.IssueToken();
            return Results.Ok(new SseTokenResponse(token, expiresAt));
        })
        .WithName("IssueSseToken")
        .WithSummary("Issue a short-lived SSE token (TTL configured via SseToken:TtlMinutes)")
        .RequireAuthorization("Operator");

        return group;
    }
}
