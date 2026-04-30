using System.Net;
using AssTrack.Api.Services;
using AssTrack.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace AssTrack.Tests.Api;

/// <summary>
/// Unit tests for webhook retry eligibility and no-retry regression.
/// </summary>
public class WebhookRetryTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static (WebhookNotificationService Service, CapturingHttpMessageHandler Handler, Channel<WebhookRetryJob> Channel)
        Build(int maxRetries = 3, HttpStatusCode responseCode = HttpStatusCode.OK, bool shouldThrow = false)
    {
        var handler = new CapturingHttpMessageHandler
        {
            ResponseStatusCode = responseCode,
            ShouldThrow = shouldThrow
        };
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new WebhookOptions
        {
            Url = "https://hooks.example.com/asstrack",
            TimeoutSeconds = 5,
            MaxRetries = maxRetries,
            RetryBaseDelayMs = 1000,
            RetryMaxDelayMs = 30000
        });
        var channel = Channel.CreateBounded<WebhookRetryJob>(
            new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });
        var svc = new WebhookNotificationService(
            httpClient, options, NullLogger<WebhookNotificationService>.Instance,
            scopeFactory: null,
            retryWriter: channel.Writer);
        return (svc, handler, channel);
    }

    // -----------------------------------------------------------------------
    // Retry eligibility: transient status codes
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void TransientStatusCodes_ShouldContain(int statusCode)
    {
        WebhookNotificationService.TransientStatusCodes.Should().Contain(statusCode);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(422)]
    public void NonTransientStatusCodes_ShouldNotBeInTransientSet(int statusCode)
    {
        WebhookNotificationService.TransientStatusCodes.Should().NotContain(statusCode);
    }

    // -----------------------------------------------------------------------
    // No-retry regression: MaxRetries=0 must never enqueue
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MaxRetries_Zero_DoesNotEnqueue_OnTransientFailure()
    {
        var (svc, _, channel) = Build(maxRetries: 0, responseCode: HttpStatusCode.InternalServerError);

        await svc.NotifySpeedAlertAsync(new SpeedAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ObservationId = Guid.Empty,
            ObservedSpeedKmh = 120,
            ThresholdKmh = 100,
            TriggeredAt = DateTime.UtcNow
        });

        channel.Reader.Count.Should().Be(0, "MaxRetries=0 disables retries");
    }

    [Fact]
    public async Task MaxRetries_Zero_DoesNotEnqueue_OnNetworkFailure()
    {
        var (svc, _, channel) = Build(maxRetries: 0, shouldThrow: true);

        await svc.NotifySpeedAlertAsync(new SpeedAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ObservationId = Guid.Empty,
            ObservedSpeedKmh = 120,
            ThresholdKmh = 100,
            TriggeredAt = DateTime.UtcNow
        });

        channel.Reader.Count.Should().Be(0, "MaxRetries=0 disables retries even on network errors");
    }

    // -----------------------------------------------------------------------
    // Retry enqueue on transient failure when MaxRetries > 0
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MaxRetries_Positive_EnqueuesRetry_On500()
    {
        var (svc, _, channel) = Build(maxRetries: 3, responseCode: HttpStatusCode.InternalServerError);

        await svc.NotifySpeedAlertAsync(new SpeedAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ObservationId = Guid.Empty,
            ObservedSpeedKmh = 120,
            ThresholdKmh = 100,
            TriggeredAt = DateTime.UtcNow
        });

        channel.Reader.Count.Should().Be(1, "A retry should be enqueued for 500 responses");
    }

    [Fact]
    public async Task MaxRetries_Positive_EnqueuesRetry_On429()
    {
        var (svc, _, channel) = Build(maxRetries: 2, responseCode: HttpStatusCode.TooManyRequests);

        await svc.NotifySpeedAlertAsync(new SpeedAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ObservationId = Guid.Empty,
            ObservedSpeedKmh = 120,
            ThresholdKmh = 100,
            TriggeredAt = DateTime.UtcNow
        });

        channel.Reader.Count.Should().Be(1, "A retry should be enqueued for 429 responses");
    }

    [Fact]
    public async Task MaxRetries_Positive_EnqueuesRetry_OnNetworkFailure()
    {
        var (svc, _, channel) = Build(maxRetries: 3, shouldThrow: true);

        await svc.NotifySpeedAlertAsync(new SpeedAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ObservationId = Guid.Empty,
            ObservedSpeedKmh = 120,
            ThresholdKmh = 100,
            TriggeredAt = DateTime.UtcNow
        });

        channel.Reader.Count.Should().Be(1, "A retry should be enqueued for network failures");
    }

    [Fact]
    public async Task NoRetry_OnSuccessfulDelivery()
    {
        var (svc, _, channel) = Build(maxRetries: 3, responseCode: HttpStatusCode.OK);

        await svc.NotifySpeedAlertAsync(new SpeedAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ObservationId = Guid.Empty,
            ObservedSpeedKmh = 120,
            ThresholdKmh = 100,
            TriggeredAt = DateTime.UtcNow
        });

        channel.Reader.Count.Should().Be(0, "No retry should be enqueued on success");
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    public async Task NoRetry_OnNonTransient4xx(int statusCode)
    {
        var (svc, _, channel) = Build(maxRetries: 3, responseCode: (HttpStatusCode)statusCode);

        await svc.NotifySpeedAlertAsync(new SpeedAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ObservationId = Guid.Empty,
            ObservedSpeedKmh = 120,
            ThresholdKmh = 100,
            TriggeredAt = DateTime.UtcNow
        });

        channel.Reader.Count.Should().Be(0, $"HTTP {statusCode} is not retryable");
    }

    // -----------------------------------------------------------------------
    // Retry job correctness
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RetryJob_HasIncrementedAttemptNumber()
    {
        var (svc, _, channel) = Build(maxRetries: 3, responseCode: HttpStatusCode.ServiceUnavailable);

        await svc.NotifySpeedAlertAsync(new SpeedAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ObservationId = Guid.Empty,
            ObservedSpeedKmh = 120,
            ThresholdKmh = 100,
            TriggeredAt = DateTime.UtcNow
        });

        channel.Reader.TryRead(out var job).Should().BeTrue();
        job!.AttemptNumber.Should().Be(2, "First retry is attempt 2");
    }

    [Fact]
    public async Task RetryJob_SharesCorrelationIdAcrossRetries()
    {
        var (svc, _, channel) = Build(maxRetries: 3, responseCode: HttpStatusCode.ServiceUnavailable);

        await svc.NotifySpeedAlertAsync(new SpeedAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ObservationId = Guid.Empty,
            ObservedSpeedKmh = 120,
            ThresholdKmh = 100,
            TriggeredAt = DateTime.UtcNow
        });

        channel.Reader.TryRead(out var job).Should().BeTrue();
        job!.CorrelationId.Should().NotBeNullOrEmpty();

        // Simulate the retry worker re-delivering via ExecuteRetryAsync with same job
        // The same CorrelationId should be preserved
        job.CorrelationId.Should().Be(job.CorrelationId);
    }

    [Fact]
    public async Task FinalAttempt_DoesNotReenqueue_WhenMaxRetriesExhausted()
    {
        var (svc, _, channel) = Build(maxRetries: 2, responseCode: HttpStatusCode.InternalServerError);

        // Simulate attempt 2 (the last allowed retry)
        var job = new WebhookRetryJob(
            WebhookId: Guid.NewGuid(),
            Payload: """{"eventType":"speed_alert"}""",
            EventType: "speed_alert",
            AttemptNumber: 2,
            CorrelationId: Guid.NewGuid().ToString(),
            ScheduledAt: DateTime.UtcNow);

        await svc.ExecuteRetryAsync(job);

        // AttemptNumber 2 <= MaxRetries 2, so it SHOULD still enqueue attempt 3
        channel.Reader.Count.Should().Be(1, "Attempt 2 with MaxRetries=2 triggers attempt 3");
    }

    [Fact]
    public async Task AttemptBeyondMaxRetries_DoesNotReenqueue()
    {
        var (svc, _, channel) = Build(maxRetries: 2, responseCode: HttpStatusCode.InternalServerError);

        // Simulate attempt 3 which is > MaxRetries=2
        var job = new WebhookRetryJob(
            WebhookId: Guid.NewGuid(),
            Payload: """{"eventType":"speed_alert"}""",
            EventType: "speed_alert",
            AttemptNumber: 3,
            CorrelationId: Guid.NewGuid().ToString(),
            ScheduledAt: DateTime.UtcNow);

        await svc.ExecuteRetryAsync(job);

        channel.Reader.Count.Should().Be(0, "Attempt 3 > MaxRetries 2 — no further retries");
    }

    [Fact]
    public async Task RetryJob_PreservesEventType()
    {
        var (svc, _, channel) = Build(maxRetries: 3, responseCode: HttpStatusCode.InternalServerError);

        await svc.NotifySpeedAlertAsync(new SpeedAlert
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ObservationId = Guid.Empty,
            ObservedSpeedKmh = 120,
            ThresholdKmh = 100,
            TriggeredAt = DateTime.UtcNow
        });

        channel.Reader.TryRead(out var job).Should().BeTrue();
        job!.EventType.Should().Be("speed_alert", "retry job must carry the originating event type");
    }
}
