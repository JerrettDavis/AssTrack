using System.Net;
using System.Net.Http.Json;
using AssTrack.Api.Services;
using AssTrack.Domain.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AssTrack.Tests.Api;

public sealed class WebhookSubscriptionDeliveryFactory : TestWebApplicationFactory
{
    public CapturingHttpMessageHandler WebhookHandler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWebhookNotificationService>();
            services.AddHttpClient<IWebhookNotificationService, WebhookNotificationService>()
                .ConfigurePrimaryHttpMessageHandler(() => WebhookHandler);
        });
    }
}

public class WebhookSubscriptionTests(WebhookSubscriptionDeliveryFactory factory) : IClassFixture<WebhookSubscriptionDeliveryFactory>
{
    [Fact]
    public async Task WebhookSubscription_CanBeCreated_AndListed()
    {
        await factory.ResetDatabaseAsync();
        factory.WebhookHandler.Reset();
        using var client = factory.CreateAuthenticatedClient();

        var create = await client.PostAsJsonAsync("/api/webhooks/subscriptions", new
        {
            name = "Enterprise hooks",
            isEnabled = true,
            eventTypes = "enterprise_signal,speed_alert",
            targetUrl = "https://hooks.example.com/enterprise",
            signingSecret = "subscription-secret"
        });

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<WebhookSubscriptionDto>();
        created.Should().NotBeNull();
        created!.Name.Should().Be("Enterprise hooks");
        created.EventTypes.Should().Be("enterprise_signal,speed_alert");
        created.SigningEnabled.Should().BeTrue();

        var subscriptions = await client.GetFromJsonAsync<List<WebhookSubscriptionDto>>("/api/webhooks/subscriptions");
        subscriptions.Should().ContainSingle(x => x.Id == created.Id && x.TargetUrl == "https://hooks.example.com/enterprise");
    }

    [Fact]
    public async Task WebhookSubscription_Changes_AreAudited()
    {
        await factory.ResetDatabaseAsync();
        factory.WebhookHandler.Reset();
        using var client = factory.CreateAuthenticatedClient();

        var create = await client.PostAsJsonAsync("/api/webhooks/subscriptions", new
        {
            name = "Enterprise hooks",
            isEnabled = true,
            eventTypes = "enterprise_signal",
            targetUrl = "https://hooks.example.com/enterprise"
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<WebhookSubscriptionDto>();

        var update = await client.PutAsJsonAsync($"/api/webhooks/subscriptions/{created!.Id}", new
        {
            name = "Enterprise hooks updated",
            isEnabled = false,
            eventTypes = "enterprise_signal,speed_alert",
            targetUrl = "https://hooks.example.com/enterprise-updated",
            signingSecret = "updated-secret"
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var delete = await client.DeleteAsync($"/api/webhooks/subscriptions/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var audit = await client.GetFromJsonAsync<PagedResult<AuditEventDto>>("/api/audit-events?entityType=webhook_subscription&pageSize=100");
        audit.Should().NotBeNull();
        audit!.Items.Should().Contain(x => x.Action == "webhook_subscription.created" && x.EntityId == created.Id.ToString());
        audit.Items.Should().Contain(x => x.Action == "webhook_subscription.updated" && x.EntityId == created.Id.ToString());
        audit.Items.Should().Contain(x => x.Action == "webhook_subscription.deleted" && x.EntityId == created.Id.ToString());
    }

    [Fact]
    public async Task IntegrationEvent_DeliversToMatchingWebhookSubscription()
    {
        await factory.ResetDatabaseAsync();
        factory.WebhookHandler.Reset();
        factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;
        factory.WebhookHandler.ShouldThrow = false;
        using var client = factory.CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/webhooks/subscriptions", new
        {
            name = "Enterprise signal hook",
            isEnabled = true,
            eventTypes = "enterprise_signal",
            targetUrl = "https://hooks.example.com/signals",
            signingSecret = "subscription-secret"
        });

        var signal = await client.PostAsJsonAsync("/api/integration-events", new
        {
            source = "servicenow",
            eventType = "ticket.created",
            severity = "warning",
            subjectType = "asset",
            subjectName = "Generator A",
            message = "Ticket opened.",
            payload = new { ticket = "INC-7" }
        });

        signal.StatusCode.Should().Be(HttpStatusCode.Created);
        factory.WebhookHandler.LastRequest.Should().NotBeNull();
        factory.WebhookHandler.LastRequest!.RequestUri!.ToString().Should().Be("https://hooks.example.com/signals");
        factory.WebhookHandler.LastRequest.Headers.Contains("X-AssTrack-Signature-256").Should().BeTrue();
        factory.WebhookHandler.LastRequestBody.Should().Contain("enterprise_signal");
        factory.WebhookHandler.LastRequestBody.Should().Contain("ticket.created");

        var logs = await client.GetFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>("/api/webhooks/deliveries?eventType=enterprise_signal");
        logs.Should().NotBeNull();
        logs!.Items.Should().ContainSingle(x =>
            x.TargetUrl == "https://hooks.example.com/signals" &&
            x.Success);
    }

    [Fact]
    public async Task WebhookSubscription_TestFiresOnlySelectedTarget_AndAudits()
    {
        await factory.ResetDatabaseAsync();
        factory.WebhookHandler.Reset();
        factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;
        factory.WebhookHandler.ShouldThrow = false;
        using var client = factory.CreateAuthenticatedClient();

        var selectedCreate = await client.PostAsJsonAsync("/api/webhooks/subscriptions", new
        {
            name = "Selected signal hook",
            isEnabled = true,
            eventTypes = "enterprise_signal",
            targetUrl = "https://hooks.example.com/selected",
            signingSecret = "selected-secret"
        });
        selectedCreate.StatusCode.Should().Be(HttpStatusCode.Created);
        var selected = await selectedCreate.Content.ReadFromJsonAsync<WebhookSubscriptionDto>();

        var otherCreate = await client.PostAsJsonAsync("/api/webhooks/subscriptions", new
        {
            name = "Other signal hook",
            isEnabled = true,
            eventTypes = "enterprise_signal",
            targetUrl = "https://hooks.example.com/other",
            signingSecret = "other-secret"
        });
        otherCreate.StatusCode.Should().Be(HttpStatusCode.Created);

        factory.WebhookHandler.Reset();

        var response = await client.PostAsync($"/api/webhooks/subscriptions/{selected!.Id}/test", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<WebhookSubscriptionTestResponse>();
        result.Should().NotBeNull();
        result!.Fired.Should().BeTrue();
        result.SubscriptionId.Should().Be(selected.Id);
        result.EventType.Should().Be("enterprise_signal");
        result.TargetUrl.Should().Be("https://hooks.example.com/selected");

        factory.WebhookHandler.LastRequest.Should().NotBeNull();
        factory.WebhookHandler.LastRequest!.RequestUri!.ToString().Should().Be("https://hooks.example.com/selected");
        factory.WebhookHandler.LastRequest.Headers.Contains("X-AssTrack-Signature-256").Should().BeTrue();
        factory.WebhookHandler.LastRequestBody.Should().Contain("enterprise_signal");
        factory.WebhookHandler.LastRequestBody.Should().Contain("Selected signal hook");

        var logs = await client.GetFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>("/api/webhooks/deliveries?eventType=enterprise_signal&pageSize=100");
        logs.Should().NotBeNull();
        logs!.Items.Should().ContainSingle(x => x.TargetUrl == "https://hooks.example.com/selected" && x.Success);
        logs.Items.Should().NotContain(x => x.TargetUrl == "https://hooks.example.com/other");

        var audit = await client.GetFromJsonAsync<PagedResult<AuditEventDto>>("/api/audit-events?action=webhook_subscription.tested&pageSize=100");
        audit.Should().NotBeNull();
        audit!.Items.Should().ContainSingle(x => x.EntityId == selected.Id.ToString());
    }

    [Fact]
    public async Task WebhookStatus_IsConfigured_WhenEnabledSubscriptionExists()
    {
        await factory.ResetDatabaseAsync();
        factory.WebhookHandler.Reset();
        using var client = factory.CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/webhooks/subscriptions", new
        {
            name = "All events",
            isEnabled = true,
            eventTypes = "*",
            targetUrl = "https://hooks.example.com/all"
        });

        var status = await client.GetFromJsonAsync<WebhookStatusDto>("/api/webhooks/status");

        status.Should().NotBeNull();
        status!.Configured.Should().BeTrue();
        status.EnabledSubscriptions.Should().Be(1);
    }

    [Fact]
    public async Task DisabledSubscription_DoesNotReceiveDelivery()
    {
        await factory.ResetDatabaseAsync();
        factory.WebhookHandler.Reset();
        using var client = factory.CreateAuthenticatedClient();

        await client.PostAsJsonAsync("/api/webhooks/subscriptions", new
        {
            name = "Disabled hook",
            isEnabled = false,
            eventTypes = "enterprise_signal",
            targetUrl = "https://hooks.example.com/disabled"
        });

        var signal = await client.PostAsJsonAsync("/api/integration-events", new
        {
            source = "erp",
            eventType = "work_order.created",
            severity = "info",
            message = "No delivery expected."
        });

        signal.StatusCode.Should().Be(HttpStatusCode.Created);
        factory.WebhookHandler.LastRequest.Should().BeNull();
        var logs = await client.GetFromJsonAsync<PagedResult<WebhookDeliveryLogDto>>("/api/webhooks/deliveries?eventType=enterprise_signal");
        logs!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task WebhookSubscription_ListIncludesHealthFromDeliveryLogs()
    {
        await factory.ResetDatabaseAsync();
        factory.WebhookHandler.Reset();
        factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        using var client = factory.CreateAuthenticatedClient();

        var create = await client.PostAsJsonAsync("/api/webhooks/subscriptions", new
        {
            name = "Signal health hook",
            isEnabled = true,
            eventTypes = "enterprise_signal",
            targetUrl = "https://hooks.example.com/health"
        });
        var subscription = await create.Content.ReadFromJsonAsync<WebhookSubscriptionDto>();

        await client.PostAsJsonAsync("/api/integration-events", new
        {
            source = "ops",
            eventType = "signal.health",
            severity = "warning",
            message = "First attempt fails."
        });

        var subscriptions = await client.GetFromJsonAsync<List<WebhookSubscriptionDto>>("/api/webhooks/subscriptions");
        var failing = subscriptions!.Single(x => x.Id == subscription!.Id);
        failing.Health.Should().Be("failing");
        failing.Last24hDeliveries.Should().Be(1);
        failing.Last24hFailures.Should().Be(1);
        failing.LastFailureAt.Should().NotBeNull();
        failing.LastHttpStatusCode.Should().Be(500);

        factory.WebhookHandler.Reset();
        factory.WebhookHandler.ResponseStatusCode = HttpStatusCode.OK;
        await client.PostAsJsonAsync("/api/integration-events", new
        {
            source = "ops",
            eventType = "signal.health",
            severity = "info",
            message = "Second attempt succeeds."
        });

        subscriptions = await client.GetFromJsonAsync<List<WebhookSubscriptionDto>>("/api/webhooks/subscriptions");
        var degraded = subscriptions!.Single(x => x.Id == subscription!.Id);
        degraded.Health.Should().Be("degraded");
        degraded.Last24hDeliveries.Should().Be(2);
        degraded.Last24hFailures.Should().Be(1);
        degraded.LastSuccessAt.Should().NotBeNull();
        degraded.LastHttpStatusCode.Should().Be(200);
    }
}
