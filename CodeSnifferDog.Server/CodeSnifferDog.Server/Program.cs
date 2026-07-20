using CodeSnifferDog.Server;
using CodeSnifferDog.Server.Components;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Endpoints;
using CodeSnifferDog.Server.Hubs;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Shared.Projects;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Serilog;

string logFilePath = Path.Combine(AppContext.BaseDirectory, "logs", "codesnifferdog-.log");
const string LogOutputTemplate =
    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}";

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog((services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: LogOutputTemplate)
        .WriteTo.File(
            logFilePath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: LogOutputTemplate));

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

await DatabaseMigrator.MigrateAsync(app.Services).ConfigureAwait(false);

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
if (app.Configuration.GetValue<bool>("HttpsRedirection:Enabled"))
    app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapProjectEndpoints();
app.MapHub<ProjectUpdatesHub>(ProjectUpdatesContract.HubPath);
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CodeSnifferDog.Server.Client.AssemblyMarker).Assembly);

app.Logger.LogInformation(
    "CodeSnifferDog server starting in {EnvironmentName}. File logs path: {LogFilePath}",
    app.Environment.EnvironmentName,
    logFilePath);
await app.RunAsync().ConfigureAwait(false);
