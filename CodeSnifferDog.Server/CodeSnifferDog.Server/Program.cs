using CodeSnifferDog.Server.Components;
using CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Client.Services.Projects;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Endpoints;
using CodeSnifferDog.Server.Hubs;
using CodeSnifferDog.Server.Services.ProjectExecution;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Services.ProjectAgentSnapshots;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.ProjectReports;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.ProjectStorage;
using CodeSnifferDog.Server.Shared.Projects;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
builder.Services.AddSignalR();
builder.Services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CodeSnifferDogServer")));
builder.Services.Configure<ProjectExecutionOptions>(
    builder.Configuration.GetSection(ProjectExecutionOptions.SectionName));
builder.Services.AddOptions<InferenceProviderOptions>()
    .Bind(builder.Configuration.GetSection(InferenceProviderOptions.SectionName))
    .PostConfigure(options =>
    {
        options.OpenAICompatible.ExtraBody = OpenAICompatibleInferenceProviderOptions.ParseExtraBody(
            builder.Configuration
                .GetSection(InferenceProviderOptions.SectionName)
                .GetSection(nameof(InferenceProviderOptions.OpenAICompatible))
                .GetSection(nameof(OpenAICompatibleInferenceProviderOptions.ExtraBody)));
    });
builder.Services.AddSingleton<ProjectTemporaryStoragePaths>();
builder.Services.AddSingleton<IProjectExecutionLeaseRegistry, ProjectExecutionLeaseRegistry>();
builder.Services.AddSingleton<IProjectExecutionQueueLock, ProjectExecutionQueueLock>();
builder.Services.AddSingleton<IProjectChatClientProvider, ProjectChatClientProvider>();
builder.Services.AddSingleton<IReviewRuleMarkdownProvider, FileSystemReviewRuleMarkdownProvider>();
builder.Services.AddScoped<ProjectReviewAgentCompactionOptionsFactory>();
builder.Services.AddScoped<IProjectReviewWorkflowRunnerFactory, ProjectReviewWorkflowRunnerFactory>();
builder.Services.AddScoped<IProjectReviewAgentTeamDependenciesFactory, ProjectReviewAgentTeamDependenciesFactory>();
builder.Services.AddScoped<IProjectReviewAgentTeamWorkerFactory, ProjectReviewAgentTeamWorkerFactory>();
builder.Services.AddScoped<IProjectReviewAnalysisExecutor, ProjectReviewAnalysisExecutor>();
builder.Services.AddScoped<IProjectAnalysisCompletionService, ProjectAnalysisCompletionService>();
builder.Services.AddScoped<IProjectAnalysisRunner, ProjectAnalysisRunner>();
builder.Services.AddScoped<IProjectAgentStatusSnapshotService, ProjectAgentStatusSnapshotService>();
builder.Services.AddScoped<IProjectAgentStatusLiveBackfillService, ProjectAgentStatusLiveBackfillService>();
builder.Services.AddScoped<IProjectAgentStatusLiveSubscriptionClient, NoOpProjectAgentStatusLiveSubscriptionClient>();
builder.Services.AddScoped<IProjectSidebarController, ServerPrerenderProjectSidebarController>();
builder.Services.AddScoped<IProjectIntakeService, ProjectIntakeService>();
builder.Services.AddScoped<IProjectReportService, ProjectReportService>();
builder.Services.AddScoped<IProjectChangePublisher, ProjectChangePublisher>();
builder.Services.AddScoped<IProjectSidebarSnapshotService, ProjectSidebarSnapshotService>();
builder.Services.AddSingleton<IProjectUpdatesNotifier, SignalRProjectUpdatesNotifier>();
builder.Services.AddSingleton<IProjectAgentStatusLiveUpdateNotifier, SignalRProjectAgentStatusLiveUpdateNotifier>();
builder.Services.AddHostedService<ProjectExecutionHostedService>();
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
