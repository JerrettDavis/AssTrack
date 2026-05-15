using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using FluentAssertions;

namespace AssTrack.Tests.Api;

public class AuditApiTests
{
    [Fact]
    public async Task AuditEvents_WithSeparateOperatorKey_Returns403()
    {
        await using var factory = new TestWebApplicationFactory(null, new Dictionary<string, string?>
        {
            ["Auth:AdminApiKey"] = "test-admin-key"
        });
        await factory.ResetDatabaseAsync();
        using var operatorClient = factory.CreateAuthenticatedClient();

        var response = await operatorClient.GetAsync("/api/audit-events");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuditEvents_WithAdminKey_ReturnsAuditLog()
    {
        await using var factory = new TestWebApplicationFactory(null, new Dictionary<string, string?>
        {
            ["Auth:AdminApiKey"] = "test-admin-key"
        });
        await factory.ResetDatabaseAsync();
        using var adminClient = factory.CreateClientWithApiKey("test-admin-key");

        var response = await adminClient.GetAsync("/api/audit-events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<AuditEventDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(50);
    }

    [Fact]
    public async Task AlertRoute_Create_RecordsAuditEvent()
    {
        await using var factory = new TestWebApplicationFactory(null, new Dictionary<string, string?>
        {
            ["Auth:AdminApiKey"] = "test-admin-key"
        });
        await factory.ResetDatabaseAsync();
        using var operatorClient = factory.CreateAuthenticatedClient();
        using var adminClient = factory.CreateClientWithApiKey("test-admin-key");

        var createResponse = await operatorClient.PostAsJsonAsync("/api/alert-routes", new
        {
            name = "Audit Dispatch",
            isEnabled = true,
            eventType = "speed_alert",
            channel = "direct",
            provider = "meshtastic",
            externalPeerId = "!audit",
            displayName = "Audit Dispatch"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await adminClient.GetAsync("/api/audit-events?action=alert_route.created&entityType=alert_route");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<AuditEventDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().ContainSingle(auditEvent =>
            auditEvent.Action == "alert_route.created" &&
            auditEvent.EntityType == "alert_route" &&
            auditEvent.EntityName == "Audit Dispatch" &&
            auditEvent.ActorRole == "operator" &&
            auditEvent.MetadataJson != null &&
            auditEvent.MetadataJson.Contains("speed_alert", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AuditEvents_Export_ReturnsCsvForAdmin()
    {
        await using var factory = new TestWebApplicationFactory(null, new Dictionary<string, string?>
        {
            ["Auth:AdminApiKey"] = "test-admin-key"
        });
        await factory.ResetDatabaseAsync();
        using var operatorClient = factory.CreateAuthenticatedClient();
        using var adminClient = factory.CreateClientWithApiKey("test-admin-key");

        var createResponse = await operatorClient.PostAsJsonAsync("/api/alert-routes", new
        {
            name = "CSV Audit Dispatch",
            isEnabled = true,
            eventType = "speed_alert",
            channel = "direct",
            provider = "meshtastic",
            externalPeerId = "!csv-audit",
            displayName = "CSV Audit Dispatch"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await adminClient.GetAsync("/api/audit-events/export?entityType=alert_route");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("id,occurredAt,actorName,actorRole,action,entityType");
        csv.Should().Contain("alert_route.created");
        csv.Should().Contain("CSV Audit Dispatch");
    }
}
