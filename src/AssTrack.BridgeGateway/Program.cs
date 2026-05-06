using AssTrack.BridgeGateway.Adapters;
using AssTrack.BridgeGateway.Endpoints;
using AssTrack.BridgeGateway.Services;

namespace AssTrack.BridgeGateway;

public static class BridgeGatewayProgram
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<BridgeGatewayOptions>(builder.Configuration.GetSection(BridgeGatewayOptions.SectionName));
        builder.Services.AddHttpClient<IAssTrackIngestClient, AssTrackIngestClient>();
        builder.Services.AddHttpClient<BridgeConfigRefreshService>();
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<IProviderPayloadAdapter, NormalizedJsonAdapter>();
        builder.Services.AddSingleton<IProviderPayloadAdapter, OwnTracksAdapter>();
        builder.Services.AddSingleton<IProviderPayloadAdapter, TraccarAdapter>();
        builder.Services.AddSingleton<IProviderPayloadAdapter, MeshtasticAdapter>();
        builder.Services.AddSingleton<ProviderAdapterRegistry>();
        builder.Services.AddSingleton<DynamicBridgeFeedStore>();
        builder.Services.AddSingleton<BridgeFeedMonitor>();
        builder.Services.AddSingleton<BridgeRequestHandler>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<BridgeConfigRefreshService>());
        builder.Services.AddHostedService<HomeAssistantPollingService>();
        builder.Services.AddHostedService<MeshtasticMqttService>();
        builder.Services.AddHealthChecks();

        var app = builder.Build();

        app.MapHealthChecks("/healthz");
        app.MapGet("/", () => Results.Redirect("/bridge/providers"));
        app.MapBridgeGatewayEndpoints();

        app.Run();
    }
}
