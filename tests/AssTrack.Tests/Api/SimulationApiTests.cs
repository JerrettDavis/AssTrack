using System.Net;
using System.Net.Http.Json;
using AssTrack.Domain.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AssTrack.Tests.Api;

public class SimulationApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SimulationApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Simulate_NormalRoute_CreatesObservations_AndReturnsCorrectCount()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var request = new SimulateRequest(SimulationPreset.NormalRoute);
        var response = await client.PostAsJsonAsync("/api/observations/simulate", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SimulateResult>();
        result.Should().NotBeNull();
        result!.ObservationsCreated.Should().Be(10);
        result.SpeedAlertsTriggered.Should().Be(0);
    }

    [Fact]
    public async Task Simulate_SpeedViolation_TriggersSpeedAlert()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var request = new SimulateRequest(SimulationPreset.SpeedViolation);
        var response = await client.PostAsJsonAsync("/api/observations/simulate", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SimulateResult>();
        result.Should().NotBeNull();
        result!.SpeedAlertsTriggered.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Simulate_GeofenceEntryExit_TriggersBreachEvents()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        var request = new SimulateRequest(SimulationPreset.GeofenceEntryExit);
        var response = await client.PostAsJsonAsync("/api/observations/simulate", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SimulateResult>();
        result.Should().NotBeNull();
        result!.GeofenceBreaches.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Simulate_WhenDisabled_Returns403()
    {
        await _factory.ResetDatabaseAsync();

        using var disabledFactory = new DisabledSimulationWebApplicationFactory();
        using var client = disabledFactory.CreateAuthenticatedClient();

        var request = new SimulateRequest(SimulationPreset.NormalRoute);
        var response = await client.PostAsJsonAsync("/api/observations/simulate", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Simulate_WithCustomDeviceIdentifier_UsesExistingOrCreatesDevice()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateAuthenticatedClient();

        const string customIdentifier = "my-custom-sim-device";
        var request = new SimulateRequest(SimulationPreset.NormalRoute, DeviceIdentifier: customIdentifier);
        var response = await client.PostAsJsonAsync("/api/observations/simulate", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SimulateResult>();
        result.Should().NotBeNull();
        result!.DeviceIdentifier.Should().Be(customIdentifier);
    }
}

/// <summary>
/// Factory that disables the Simulation feature for testing the 403 guard.
/// </summary>
file sealed class DisabledSimulationWebApplicationFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Simulation:Enabled"] = "false"
            });
        });
    }
}
