using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace AssTrack.Api.Services;

/// <summary>
/// In-memory implementation of <see cref="ISseTokenService"/>.
/// Tokens are cryptographically random strings with a configurable TTL (default 10 minutes).
/// </summary>
public sealed class SseTokenService : ISseTokenService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _tokens = new();
    private readonly int _ttlMinutes;

    public SseTokenService(IConfiguration configuration)
    {
        _ttlMinutes = configuration.GetValue<int>("SseToken:TtlMinutes", 10);
    }

    public (string Token, DateTimeOffset ExpiresAt) IssueToken()
    {
        LazyCleanup();
        var token = CreateToken();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_ttlMinutes);
        _tokens[token] = expiresAt;
        return (token, expiresAt);
    }

    public string GenerateToken()
    {
        var (token, _) = IssueToken();
        return token;
    }

    public bool ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (_tokens.TryGetValue(token, out var expiresAt))
        {
            if (expiresAt > DateTimeOffset.UtcNow)
                return true;

            _tokens.TryRemove(token, out _);
        }

        return false;
    }

    private static string CreateToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private void LazyCleanup()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _tokens.Keys.ToList())
        {
            if (_tokens.TryGetValue(key, out var exp) && exp <= now)
                _tokens.TryRemove(key, out _);
        }
    }
}
