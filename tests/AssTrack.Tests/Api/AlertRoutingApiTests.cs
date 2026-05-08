using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssTrack.Tests.Api;

public class AlertRoutingApiTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task AlertRoute_CanBeCreated_AndListed()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        var feedId = await CreateFeedAsync(client);

        var response = await client.PostAsJsonAsync("/api/alert-routes", new
        {
            name = "Dispatch",
            isEnabled = true,
            eventType = "speed_alert",
            channel = "direct",
            provider = "meshtastic",
            integrationFeedId = feedId,
            externalPeerId = "!12f4fb74",
            displayName = "Dispatch"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var routes = await client.GetFromJsonAsync<List<AlertRoutingRuleDto>>("/api/alert-routes");

        routes.Should().ContainSingle(route =>
            route.Name == "Dispatch" &&
            route.EventType == "speed_alert" &&
            route.IntegrationFeedId == feedId);
    }

    [Fact]
    public async Task SpeedAlert_Route_QueuesOutboundMessage()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();
        var feedId = await CreateFeedAsync(client);

        var routeResponse = await client.PostAsJsonAsync("/api/alert-routes", new
        {
            name = "Ops",
            isEnabled = true,
            eventType = "speed_alert",
            channel = "direct",
            provider = "meshtastic",
            integrationFeedId = feedId,
            externalPeerId = "!12f4fb74",
            displayName = "Ops"
        });
        routeResponse.EnsureSuccessStatusCode();

        Guid deviceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
            var asset = new Asset { Name = "Truck 17", SpeedThresholdKmh = 80 };
            var device = new Device { Identifier = "truck-17-gps", Asset = asset };
            db.Assets.Add(asset);
            db.Devices.Add(device);
            await db.SaveChangesAsync();
            deviceId = device.Id;
        }

        var ingestResponse = await client.PostAsJsonAsync("/api/observations", new CreateObservationRequest(
            deviceId,
            DateTime.UtcNow,
            41.8781,
            -87.6298,
            null,
            10,
            95,
            null,
            null));

        ingestResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var message = await verificationDb.MessageEntries
            .Include(x => x.Thread)
            .SingleAsync(x => x.Direction == MessageDirection.Outbound);

        message.Status.Should().Be(MessageStatus.Queued);
        message.Body.Should().Contain("Truck 17");
        message.Thread!.IntegrationFeedId.Should().Be(feedId);
        message.Thread.ExternalPeerId.Should().Be("!12f4fb74");
    }

    private static async Task<Guid> CreateFeedAsync(HttpClient client)
    {
        var feedResponse = await client.PostAsJsonAsync("/api/integrations", new
        {
            name = "Meshtastic alert routing",
            provider = "meshtastic",
            isEnabled = true,
            autoCreateDevices = true,
            defaultTags = "mesh"
        });
        feedResponse.EnsureSuccessStatusCode();
        var feed = await feedResponse.Content.ReadFromJsonAsync<JsonElement>();
        return feed.GetProperty("id").GetGuid();
    }
}
