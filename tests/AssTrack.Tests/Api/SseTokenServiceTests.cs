using AssTrack.Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AssTrack.Tests.Api;

public class SseTokenServiceTests
{
    private static SseTokenService CreateService(int ttlMinutes = 10)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SseToken:TtlMinutes"] = ttlMinutes.ToString()
            })
            .Build();
        return new SseTokenService(config);
    }

    [Fact]
    public void IssueToken_ReturnsNonEmptyToken()
    {
        var service = CreateService();
        var (token, expiresAt) = service.IssueToken();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(expiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void IssueToken_ExpiresAtReflectsTtl()
    {
        var service = CreateService(ttlMinutes: 5);
        var before = DateTimeOffset.UtcNow;
        var (_, expiresAt) = service.IssueToken();
        var after = DateTimeOffset.UtcNow;

        Assert.True(expiresAt >= before.AddMinutes(5));
        Assert.True(expiresAt <= after.AddMinutes(5).AddSeconds(1));
    }

    [Fact]
    public void GenerateToken_ReturnsValidToken()
    {
        var service = CreateService();
        var token = service.GenerateToken();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(service.ValidateToken(token));
    }

    [Fact]
    public void ValidateToken_WithValidToken_ReturnsTrue()
    {
        var service = CreateService();
        var (token, _) = service.IssueToken();
        Assert.True(service.ValidateToken(token));
    }

    [Fact]
    public void ValidateToken_WithUnknownToken_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.ValidateToken("unknown-token"));
    }

    [Fact]
    public void ValidateToken_WithEmptyToken_ReturnsFalse()
    {
        var service = CreateService();
        Assert.False(service.ValidateToken(string.Empty));
    }

    [Fact]
    public async Task ValidateToken_WithExpiredToken_ReturnsFalse()
    {
        var service = CreateService(ttlMinutes: 0);
        var (token, _) = service.IssueToken();

        // TTL is 0 minutes — expires immediately
        await Task.Delay(50);

        Assert.False(service.ValidateToken(token));
    }

    [Fact]
    public void IssueToken_EachCallProducesUniqueToken()
    {
        var service = CreateService();
        var (token1, _) = service.IssueToken();
        var (token2, _) = service.IssueToken();
        Assert.NotEqual(token1, token2);
    }
}
