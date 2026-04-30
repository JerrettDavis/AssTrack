using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AssTrack.Tests.Api;

public class RateLimitedTestWebApplicationFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:IngestPermitLimit"] = "1",
                ["RateLimiting:IngestWindowSeconds"] = "60"
            });
        });
    }
}
