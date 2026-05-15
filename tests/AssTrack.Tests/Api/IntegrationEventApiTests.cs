using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using AssTrack.Domain.Models;
using AssTrack.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssTrack.Tests.Api;

public class IntegrationEventApiTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task PublishIntegrationEvent_PersistsAndAuditsEvent()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/integration-events", new
        {
            source = "servicenow",
            eventType = "ticket.created",
            severity = "warning",
            subjectType = "asset",
            subjectId = "A-100",
            subjectName = "Generator A",
            message = "Service ticket opened for field generator.",
            payload = new { ticket = "INC100", priority = 2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<IntegrationEventDto>();
        created.Should().NotBeNull();
        created!.Source.Should().Be("servicenow");
        created.ExternalEventId.Should().BeNull();
        created.EventType.Should().Be("ticket.created");
        created.Severity.Should().Be("warning");
        created.Status.Should().Be(IntegrationEventStatuses.Open);
        created.PayloadJson.Should().Contain("INC100");

        var list = await client.GetFromJsonAsync<PagedResult<IntegrationEventDto>>("/api/integration-events?source=servicenow&eventType=ticket.created");
        list.Should().NotBeNull();
        list!.Items.Should().ContainSingle(x => x.Id == created.Id);

        var audit = await client.GetFromJsonAsync<PagedResult<AuditEventDto>>("/api/audit-events?action=integration_event.published");
        audit.Should().NotBeNull();
        audit!.Items.Should().ContainSingle(x => x.EntityId == created.Id.ToString());
    }

    [Fact]
    public async Task PublishIntegrationEvent_WithExternalEventId_IsIdempotent()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();

        var request = new
        {
            source = "servicenow",
            externalEventId = "INC100-update-1",
            eventType = "ticket.updated",
            severity = "warning",
            subjectType = "asset",
            subjectId = "A-100",
            subjectName = "Generator A",
            message = "Service ticket updated.",
            payload = new { ticket = "INC100", state = "assigned" }
        };

        var firstResponse = await client.PostAsJsonAsync("/api/integration-events", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var first = await firstResponse.Content.ReadFromJsonAsync<IntegrationEventDto>();

        var duplicateResponse = await client.PostAsJsonAsync("/api/integration-events", request);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<IntegrationEventDto>();

        duplicate!.Id.Should().Be(first!.Id);
        duplicate.ExternalEventId.Should().Be("INC100-update-1");

        var list = await client.GetFromJsonAsync<PagedResult<IntegrationEventDto>>("/api/integration-events?source=servicenow&externalEventId=INC100-update-1");
        list.Should().NotBeNull();
        list!.Items.Should().ContainSingle(x => x.Id == first.Id);

        var audit = await client.GetFromJsonAsync<PagedResult<AuditEventDto>>("/api/audit-events?action=integration_event.published&pageSize=100");
        audit.Should().NotBeNull();
        audit!.Items.Count(x => x.EntityId == first.Id.ToString()).Should().Be(1);
    }

    [Fact]
    public async Task IntegrationEvents_Export_ReturnsCsvWithLifecycleAndIdempotencyFields()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();

        var publish = await client.PostAsJsonAsync("/api/integration-events", new
        {
            source = "servicenow",
            externalEventId = "INC100-export",
            eventType = "ticket.updated",
            severity = "warning",
            subjectType = "asset",
            subjectId = "A-100",
            subjectName = "Generator A",
            message = "Service ticket updated, assigned.",
            payload = new { ticket = "INC100", state = "assigned" }
        });
        publish.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await publish.Content.ReadFromJsonAsync<IntegrationEventDto>();

        var resolve = await client.PostAsJsonAsync($"/api/integration-events/{created!.Id}/resolve", new
        {
            resolutionNote = "Export proof"
        });
        resolve.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync("/api/integration-events/export?source=servicenow&externalEventId=INC100-export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().Contain("id,occurredAt,source,externalEventId,eventType,severity,status");
        csv.Should().Contain("INC100-export");
        csv.Should().Contain("resolved");
        csv.Should().Contain("Export proof");
        csv.Should().Contain("\"Service ticket updated, assigned.\"");
    }

    [Fact]
    public async Task IntegrationEvent_CanBeAcknowledgedAndResolved()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();

        var publish = await client.PostAsJsonAsync("/api/integration-events", new
        {
            source = "okta",
            eventType = "identity.user_locked",
            severity = "critical",
            subjectType = "user",
            subjectId = "u-123",
            subjectName = "Riley Stone",
            message = "User account lockout detected.",
            payload = new { risk = "high" }
        });
        publish.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await publish.Content.ReadFromJsonAsync<IntegrationEventDto>();
        created!.Status.Should().Be(IntegrationEventStatuses.Open);

        var acknowledge = await client.PostAsync($"/api/integration-events/{created.Id}/acknowledge", null);
        acknowledge.StatusCode.Should().Be(HttpStatusCode.OK);
        var acknowledged = await acknowledge.Content.ReadFromJsonAsync<IntegrationEventDto>();
        acknowledged!.Status.Should().Be(IntegrationEventStatuses.Acknowledged);
        acknowledged.AcknowledgedAt.Should().NotBeNull();
        acknowledged.AcknowledgedBy.Should().NotBeNullOrWhiteSpace();

        var open = await client.GetFromJsonAsync<PagedResult<IntegrationEventDto>>("/api/integration-events?status=open");
        open!.Items.Should().NotContain(x => x.Id == created.Id);

        var acknowledgedList = await client.GetFromJsonAsync<PagedResult<IntegrationEventDto>>("/api/integration-events?status=acknowledged");
        acknowledgedList!.Items.Should().ContainSingle(x => x.Id == created.Id);

        var resolve = await client.PostAsJsonAsync($"/api/integration-events/{created.Id}/resolve", new
        {
            resolutionNote = "Confirmed expected lockout and notified identity team."
        });
        resolve.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolved = await resolve.Content.ReadFromJsonAsync<IntegrationEventDto>();
        resolved!.Status.Should().Be(IntegrationEventStatuses.Resolved);
        resolved.ResolvedAt.Should().NotBeNull();
        resolved.ResolvedBy.Should().NotBeNullOrWhiteSpace();
        resolved.ResolutionNote.Should().Be("Confirmed expected lockout and notified identity team.");

        var resolvedList = await client.GetFromJsonAsync<PagedResult<IntegrationEventDto>>("/api/integration-events?status=resolved");
        resolvedList!.Items.Should().ContainSingle(x => x.Id == created.Id);

        var audit = await client.GetFromJsonAsync<PagedResult<AuditEventDto>>("/api/audit-events?entityType=integration_event&pageSize=100");
        audit.Should().NotBeNull();
        audit!.Items.Should().Contain(x => x.Action == "integration_event.acknowledged" && x.EntityId == created.Id.ToString());
        audit.Items.Should().Contain(x => x.Action == "integration_event.resolved" && x.EntityId == created.Id.ToString());
    }

    [Fact]
    public async Task PublishIntegrationEvent_RoutesEnterpriseSignalToMessages()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();

        var routeResponse = await client.PostAsJsonAsync("/api/alert-routes", new
        {
            name = "Enterprise Ops",
            isEnabled = true,
            eventType = AlertRouteEventTypes.EnterpriseSignal,
            channel = "direct",
            provider = "meshtastic",
            externalPeerId = "!enterprise",
            displayName = "Enterprise Ops",
            messageTemplate = "Ops signal: {message}"
        });
        routeResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var signalResponse = await client.PostAsJsonAsync("/api/integration-events", new
        {
            source = "erp",
            eventType = "work_order.created",
            severity = "critical",
            subjectType = "asset",
            subjectName = "Forklift 4",
            message = "Work order requires dispatch.",
            payload = new { workOrder = "WO-44" }
        });
        signalResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AssTrackDbContext>();
        var message = await db.MessageEntries.Include(x => x.Thread).SingleAsync(x => x.Direction == MessageDirection.Outbound);
        message.Status.Should().Be(MessageStatus.Queued);
        message.Body.Should().Contain("Ops signal:");
        message.Body.Should().Contain("Work order requires dispatch");
        message.Metadata.Should().Contain("work_order.created");
        message.Thread!.ExternalPeerId.Should().Be("!enterprise");
    }

    [Fact]
    public async Task PublishIntegrationEvent_ValidatesRequiredFields()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/integration-events", new
        {
            source = "",
            eventType = "ticket.created",
            severity = "info",
            message = "Missing source"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

public class IntegrationEventWebhookTests(WebhookDeliveryLogFactory factory) : IClassFixture<WebhookDeliveryLogFactory>
{
    [Fact]
    public async Task PublishIntegrationEvent_WhenWebhookConfigured_DeliversEnterpriseSignalWebhook()
    {
        await factory.ResetDatabaseAsync();
        factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;
        factory.WebhookHandler.ShouldThrow = false;
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/integration-events", new
        {
            source = "okta",
            eventType = "identity.user_locked",
            severity = "critical",
            subjectType = "user",
            subjectId = "u-123",
            subjectName = "Riley Stone",
            message = "User account lockout detected.",
            payload = new { risk = "high" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        factory.WebhookHandler.LastRequestBody.Should().Contain("enterprise_signal");
        factory.WebhookHandler.LastRequestBody.Should().Contain("identity.user_locked");

        var logs = await client.GetFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>("/api/webhooks/deliveries?eventType=enterprise_signal");
        logs.Should().NotBeNull();
        logs!.Items.Should().ContainSingle(x => x.Success && x.EventType == "enterprise_signal");
    }

    [Fact]
    public async Task PublishIntegrationEvent_DuplicateExternalEventId_DoesNotFanOutTwice()
    {
        await factory.ResetDatabaseAsync();
        factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;
        factory.WebhookHandler.ShouldThrow = false;
        using var client = factory.CreateAuthenticatedClient();

        var request = new
        {
            source = "okta",
            externalEventId = "evt-lockout-123",
            eventType = "identity.user_locked",
            severity = "critical",
            subjectType = "user",
            subjectId = "u-123",
            subjectName = "Riley Stone",
            message = "User account lockout detected.",
            payload = new { risk = "high" }
        };

        var firstResponse = await client.PostAsJsonAsync("/api/integration-events", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var first = await firstResponse.Content.ReadFromJsonAsync<IntegrationEventDto>();

        var duplicateResponse = await client.PostAsJsonAsync("/api/integration-events", request);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<IntegrationEventDto>();
        duplicate!.Id.Should().Be(first!.Id);

        var logs = await client.GetFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>("/api/webhooks/deliveries?eventType=enterprise_signal");
        logs.Should().NotBeNull();
        logs!.Items.Should().ContainSingle(x => x.Success && x.EventType == "enterprise_signal");
    }
}
