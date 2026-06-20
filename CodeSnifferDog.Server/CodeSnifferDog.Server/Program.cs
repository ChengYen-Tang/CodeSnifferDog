using CodeSnifferDog.Server;
using CodeSnifferDog.Server.Components;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Endpoints;
using CodeSnifferDog.Server.Hubs;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Shared.Projects;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
builder.Services.AddSignalR();
builder.Services.AddCodeSnifferDogServerServices(builder.Configuration);
builder.Services.AddScoped(sp =>
{
    NavigationManager navigationManager = sp.GetRequiredService<NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigationManager.BaseUri),
    };
});

var app = builder.Build();

await CodeSnifferDogServerDatabaseMigrator.MigrateAsync(app.Services).ConfigureAwait(false);

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
