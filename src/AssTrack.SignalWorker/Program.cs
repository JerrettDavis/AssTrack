using AssTrack.MessageBridge.Worker;
using AssTrack.SignalWorker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<BridgeWorkerOptions>(builder.Configuration.GetSection(BridgeWorkerOptions.SectionName));
builder.Services.Configure<SignalWorkerOptions>(builder.Configuration.GetSection(SignalWorkerOptions.SectionName));
builder.Services.AddHttpClient<BridgeGatewayClient>();
builder.Services.AddHttpClient<IMessageProviderClient, SignalProviderClient>();
builder.Services.AddSingleton<BridgeMessagePump>();
builder.Services.AddHostedService<BridgeMessageWorker>();

await builder.Build().RunAsync();
