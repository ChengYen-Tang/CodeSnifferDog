using CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Client.Services.Projects;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});
builder.Services.AddScoped<IProjectAgentStatusLiveSubscriptionClient, SignalRProjectAgentStatusLiveSubscriptionClient>();
builder.Services.AddScoped<IProjectSidebarRefreshSignalClient, SignalRProjectSidebarRefreshSignalClient>();
builder.Services.AddScoped<IProjectSidebarPollingFallback, PeriodicProjectSidebarPollingFallback>();
builder.Services.AddScoped<ProjectSidebarSyncService>();

await builder.Build().RunAsync();
