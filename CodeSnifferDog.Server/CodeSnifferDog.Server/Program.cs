using CodeSnifferDog.Server.Components;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Endpoints;
using CodeSnifferDog.Server.Hubs;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddSignalR();
builder.Services.AddDbContext<CodeSnifferDogServerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CodeSnifferDogServer")));
builder.Services.AddScoped<IProjectIntakeService, ProjectIntakeService>();
builder.Services.AddScoped<IProjectChangePublisher, ProjectChangePublisher>();
builder.Services.AddSingleton<IProjectUpdatesNotifier, SignalRProjectUpdatesNotifier>();
builder.Services.AddScoped(sp =>
{
    NavigationManager navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigationManager.BaseUri),
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapProjectEndpoints();
app.MapHub<ProjectUpdatesHub>(ProjectUpdatesContract.HubPath);
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CodeSnifferDog.Server.Client.AssemblyMarker).Assembly);

app.Run();
