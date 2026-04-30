namespace AssTrack.Api.Services;

public interface ISseTokenService
{
    /// <summary>Issues a new SSE token and returns it along with its expiration time.</summary>
    (string Token, DateTimeOffset ExpiresAt) IssueToken();

    /// <summary>Generates a new SSE token and returns the token string.</summary>
    string GenerateToken();

    /// <summary>Validates the given token. Returns true if the token exists and has not expired.</summary>
    bool ValidateToken(string? token);
}
