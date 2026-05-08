using AssTrack.MessageBridge.Worker;
using AssTrack.TelegramWorker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<BridgeWorkerOptions>(builder.Configuration.GetSection(BridgeWorkerOptions.SectionName));
builder.Services.Configure<TelegramWorkerOptions>(builder.Configuration.GetSection(TelegramWorkerOptions.SectionName));
builder.Services.AddHttpClient<BridgeGatewayClient>();
builder.Services.AddHttpClient<IMessageProviderClient, TelegramProviderClient>();
builder.Services.AddSingleton<BridgeMessagePump>();
builder.Services.AddHostedService<BridgeMessageWorker>();

await builder.Build().RunAsync();
