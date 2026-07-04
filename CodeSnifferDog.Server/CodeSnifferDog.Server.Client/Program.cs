using CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Client.Services.Projects.Sidebar;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// Configures the Blazor WebAssembly client service graph.
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});
builder.Services.AddScoped<ILiveSubscriptionClient, SignalRLiveSubscriptionClient>();
builder.Services.AddScoped<IRefreshSignalClient, SignalRRefreshSignalClient>();
builder.Services.AddScoped<IPollingFallback, PeriodicPollingFallback>();
builder.Services.AddScoped<IController, SyncService>();

await builder.Build().RunAsync();
