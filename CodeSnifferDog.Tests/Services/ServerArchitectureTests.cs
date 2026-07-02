using CodeSnifferDog.Server;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;
using CodeSnifferDog.Server.Services.ProjectReports.Queries;
using CodeSnifferDog.Server.Services.Projects.Sidebar;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Queries;
using CodeSnifferDog.Server.Services.Projects.Projection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using AgentStatusSnapshotReadModel = CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries.SnapshotReadModel;
using AgentStatusSnapshotService = CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.SnapshotService;
using AgentStatusSnapshotServiceInterface = CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.ISnapshotService;
using ReportExportFile = CodeSnifferDog.Server.Services.ProjectReports.Export.ExportFile;
using ReportExportService = CodeSnifferDog.Server.Services.ProjectReports.Export.ExportService;
using ReportExportServiceInterface = CodeSnifferDog.Server.Services.ProjectReports.Export.IExportService;
using ReportProjectionMapper = CodeSnifferDog.Server.Services.ProjectReports.Projection.ProjectionMapper;
using ReportProjectionMapperInterface = CodeSnifferDog.Server.Services.ProjectReports.Projection.IProjectionMapper;
using ReportProjectProjection = CodeSnifferDog.Server.Services.ProjectReports.Projection.ProjectProjection;
using ReportQueryService = CodeSnifferDog.Server.Services.ProjectReports.Queries.QueryService;
using ReportQueryServiceInterface = CodeSnifferDog.Server.Services.ProjectReports.Queries.IQueryService;
using ReportRuleReportDraft = CodeSnifferDog.Server.Services.ProjectReports.RuleReportDraft;
using ReportRuleReportProjection = CodeSnifferDog.Server.Services.ProjectReports.Projection.RuleReportProjection;
using ReportsService = CodeSnifferDog.Server.Services.ProjectReports.ReportService;
using ReportsServiceInterface = CodeSnifferDog.Server.Services.ProjectReports.IReportService;
using SidebarQueryService = CodeSnifferDog.Server.Services.Projects.Sidebar.Queries.QueryService;
using SidebarQueryServiceInterface = CodeSnifferDog.Server.Services.Projects.Sidebar.Queries.IQueryService;
using SidebarSnapshotReadModel = CodeSnifferDog.Server.Services.Projects.Sidebar.Queries.SnapshotReadModel;
using SidebarSnapshotService = CodeSnifferDog.Server.Services.Projects.Sidebar.SnapshotService;
using SidebarSnapshotServiceInterface = CodeSnifferDog.Server.Services.Projects.Sidebar.ISnapshotService;

namespace CodeSnifferDog.Tests.Services;

[TestClass]
public sealed class ServerArchitectureTests
{
    [TestMethod]
    public void SharedProjectionServices_RegisterProjectStatusMapperOnceAsSingleton()
    {
        ServiceCollection services = CreateServices();

        ServiceDescriptor descriptor = services.Single(descriptor => descriptor.ServiceType == typeof(IProjectStatusMapper));

        Assert.AreEqual(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.AreEqual(typeof(ProjectStatusMapper), descriptor.ImplementationType);
    }

    [TestMethod]
    public void ReadSideFacades_DoNotDependDirectlyOnDbContext()
    {
        Type[] facadeTypes =
        [
            typeof(AgentStatusSnapshotService),
            typeof(LiveBackfillService),
            typeof(SidebarSnapshotService),
        ];

        foreach (Type facadeType in facadeTypes)
        {
            Type[] dependencyTypes = facadeType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single()
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();

            Assert.IsFalse(
                dependencyTypes.Any(IsDbContextDependency),
                $"{facadeType.Name} should depend on query/projection collaborators instead of DbContext.");
        }
    }

    [TestMethod]
    public void ReadSideQueryInterfaces_ReturnInternalReadModelsNotSharedDtos()
    {
        Type[] queryInterfaces =
        [
            typeof(ReportQueryServiceInterface),
            typeof(ISnapshotQueryService),
            typeof(IBackfillQueryService),
            typeof(SidebarQueryServiceInterface),
        ];

        foreach (Type queryInterface in queryInterfaces)
        {
            foreach (MethodInfo method in queryInterface.GetMethods())
            {
                Assert.IsFalse(
                    ContainsSharedDto(method.ReturnType),
                    $"{queryInterface.Name}.{method.Name} should return internal read/projection models, not shared DTOs.");
            }
        }
    }

    [TestMethod]
    public void InternalServiceInterfaces_AreRegisteredOrDocumentedSeams()
    {
        ServiceCollection services = CreateServices();
        HashSet<Type> registeredInterfaces = services
            .Where(descriptor => descriptor.ServiceType.IsInterface)
            .Select(descriptor => descriptor.ServiceType)
            .ToHashSet();
        HashSet<Type> documentedSeams =
        [
            // Built by RuntimeComponentsFactory for a single project runtime.
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence.IEventHandler),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence.IPersistenceService),

            // Returned by WorkerFactory; not resolved from DI directly.
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.IWorker),
        ];

        Type[] internalInterfaces = typeof(CodeSnifferDogServerServiceCollectionExtensions)
            .Assembly
            .GetTypes()
            .Where(type =>
                type.IsInterface &&
                type.Namespace?.StartsWith("CodeSnifferDog.Server.Services", StringComparison.Ordinal) == true &&
                !type.IsPublic)
            .ToArray();

        Type[] missing = internalInterfaces
            .Where(type => !registeredInterfaces.Contains(type) && !documentedSeams.Contains(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.IsEmpty(missing, string.Join(Environment.NewLine, missing.Select(type => type.FullName)));
    }

    [TestMethod]
    public void ProjectExecutionReviewTeamWorker_UsesLocalRoleNames()
    {
        Type[] reviewTeamTypes =
        [
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.IDependenciesFactory),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.IWorker),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.IWorkerFactory),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.DependenciesFactory),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Worker),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.WorkerFactory),
        ];

        foreach (Type type in reviewTeamTypes)
        {
            Assert.AreEqual(
                "CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam",
                type.Namespace,
                $"{type.Name} should stay in the review-team worker subnamespace.");
            Assert.IsFalse(
                type.Name.StartsWith("ProjectReviewAgentTeam", StringComparison.Ordinal) ||
                type.Name.StartsWith("IProjectReviewAgentTeam", StringComparison.Ordinal),
                $"{type.Name} should rely on its folder/namespace for project review agent team context.");
        }
    }

    [TestMethod]
    public void ProjectExecutionStatusServices_UseLocalRoleNames()
    {
        Type[] statusTypes =
        [
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence.PersistenceEventHandler),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence.LiveUpdateFactory),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence.PersistenceService),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence.TimelineEntryMutationResult),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence.TimelineRemovalMutationResult),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence.TimelinePersistenceService),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence.IEventHandler),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence.IPersistenceService),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence.ITimelinePersistenceService),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime.EventSubscriber),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime.EventSubscriberFactory),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime.RuntimeContext),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime.RuntimeComponents),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime.RuntimeComponentsFactory),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime.RuntimeFactory),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime.IEventSubscriberFactory),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime.IRuntimeComponentsFactory),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime.IRuntimeFactory),
        ];

        foreach (Type type in statusTypes)
        {
            Assert.IsFalse(
                type.Name.StartsWith("AgentStatus", StringComparison.Ordinal) ||
                type.Name.StartsWith("IAgentStatus", StringComparison.Ordinal) ||
                type.Name.StartsWith("AgentTimeline", StringComparison.Ordinal) ||
                type.Name.StartsWith("IAgentTimeline", StringComparison.Ordinal) ||
                type.Name.StartsWith("ProjectAgentStatus", StringComparison.Ordinal),
                $"{type.Name} should rely on the ProjectExecution.Status namespace for status/agent context.");
        }
    }

    [TestMethod]
    public void ProjectExecutionCancellationServices_UseLocalRoleNames()
    {
        Type[] cancellationTypes =
        [
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation.Outcome),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation.Policy),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation.Source),
        ];

        foreach (Type type in cancellationTypes)
        {
            Assert.AreEqual(
                "CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation",
                type.Namespace,
                $"{type.Name} should stay in the cancellation subnamespace.");
            Assert.IsFalse(
                type.Name.StartsWith("ProjectExecutionCancellation", StringComparison.Ordinal),
                $"{type.Name} should rely on its folder/namespace for project execution cancellation context.");
        }
    }

    [TestMethod]
    public void ProjectExecutionInfrastructureServices_UseLocalRoleNames()
    {
        Type[] infrastructureTypes =
        [
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.HostedService),
        ];

        foreach (Type type in infrastructureTypes)
        {
            Assert.AreEqual(
                "CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure",
                type.Namespace,
                $"{type.Name} should stay in the project execution infrastructure namespace.");
            Assert.IsFalse(
                type.Name.StartsWith("ProjectExecution", StringComparison.Ordinal),
                $"{type.Name} should rely on its folder/namespace for project execution context.");
        }
    }

    [TestMethod]
    public void ProjectExecutionQueueServices_UseLocalRoleNames()
    {
        Type[] queueTypes =
        [
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue.Claim),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue.ExecutionQueueClaimer),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue.IExecutionQueueClaimer),
        ];

        foreach (Type type in queueTypes)
        {
            Assert.AreEqual(
                "CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue",
                type.Namespace,
                $"{type.Name} should stay in the project execution queue namespace.");
            Assert.IsFalse(
                type.Name.StartsWith("ProjectExecution", StringComparison.Ordinal),
                $"{type.Name} should rely on its folder/namespace for project execution queue context.");
        }
    }

    [TestMethod]
    public void ProjectExecutionWorkflowServices_UseLocalRoleNames()
    {
        Type[] workflowTypes =
        [
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Workflows.IReviewRunnerFactory),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Workflows.ReviewRunnerFactory),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Workflows.ReviewRunners),
        ];

        foreach (Type type in workflowTypes)
        {
            Assert.AreEqual(
                "CodeSnifferDog.Server.Services.ProjectExecution.Workflows",
                type.Namespace,
                $"{type.Name} should stay in the project execution workflows namespace.");
            Assert.IsFalse(
                type.Name.StartsWith("ProjectReviewWorkflow", StringComparison.Ordinal) ||
                type.Name.StartsWith("IProjectReviewWorkflow", StringComparison.Ordinal),
                $"{type.Name} should rely on its folder/namespace for project review workflow context.");
        }
    }

    [TestMethod]
    public void ProjectExecutionAnalysisServices_UseLocalRoleNames()
    {
        Type[] analysisTypes =
        [
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Analysis.ICompletionService),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Analysis.CompletionService),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Analysis.Runner),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Analysis.IReviewAnalysisExecutor),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Analysis.ReviewAnalysisExecutor),
        ];

        foreach (Type type in analysisTypes)
        {
            Assert.AreEqual(
                "CodeSnifferDog.Server.Services.ProjectExecution.Analysis",
                type.Namespace,
                $"{type.Name} should stay in the project execution analysis namespace.");
            Assert.IsFalse(
                type.Name.StartsWith("ProjectAnalysis", StringComparison.Ordinal) ||
                type.Name.StartsWith("IProjectAnalysis", StringComparison.Ordinal) ||
                type.Name.StartsWith("ProjectReviewAnalysis", StringComparison.Ordinal) ||
                type.Name.StartsWith("IProjectReviewAnalysis", StringComparison.Ordinal),
                $"{type.Name} should rely on its folder/namespace for project review analysis context.");
        }
    }

    [TestMethod]
    public void ProjectAgentStatusServices_UseLocalRoleNames()
    {
        Type[] projectAgentStatusTypes =
        [
            typeof(AgentStatusSnapshotServiceInterface),
            typeof(AgentStatusSnapshotService),
            typeof(ILiveBackfillService),
            typeof(LiveBackfillService),
            typeof(ISnapshotQueryService),
            typeof(SnapshotQueryService),
            typeof(IBackfillQueryService),
            typeof(BackfillQueryService),
            typeof(AgentStatusSnapshotReadModel),
            typeof(SnapshotGroupRow),
            typeof(SnapshotAgentRow),
            typeof(HistorySnapshotReadModel),
            typeof(BackfillReadModel),
            typeof(ILiveUpdateNotifier),
            typeof(SignalRLiveUpdateNotifier),
            typeof(IProjectionMapper),
            typeof(ProjectionMapper),
            typeof(GroupProjection),
            typeof(AgentProjection),
            typeof(TimelineEntryProjection),
            typeof(ExceptionStyle),
        ];

        foreach (Type type in projectAgentStatusTypes)
        {
            Assert.IsTrue(
                type.Namespace?.StartsWith("CodeSnifferDog.Server.Services.ProjectAgentStatus", StringComparison.Ordinal) == true,
                $"{type.Name} should stay under the ProjectAgentStatus service namespace.");
            Assert.IsFalse(
                type.Name.StartsWith("ProjectAgentStatus", StringComparison.Ordinal) ||
                type.Name.StartsWith("IProjectAgentStatus", StringComparison.Ordinal) ||
                type.Name.StartsWith("AgentStatusProjection", StringComparison.Ordinal) ||
                type.Name.StartsWith("IAgentStatusProjection", StringComparison.Ordinal),
                $"{type.Name} should rely on its folder/namespace for project agent status context.");
        }
    }

    [TestMethod]
    public void ProjectSidebarServices_UseLocalRoleNames()
    {
        Type[] projectSidebarTypes =
        [
            typeof(SidebarSnapshotServiceInterface),
            typeof(SidebarSnapshotService),
            typeof(SidebarQueryServiceInterface),
            typeof(SidebarQueryService),
            typeof(SidebarSnapshotReadModel),
            typeof(GroupReadModel),
            typeof(ProjectReadModel),
            typeof(ProjectProjection),
            typeof(MappedProject),
        ];

        foreach (Type type in projectSidebarTypes)
        {
            Assert.IsTrue(
                type.Namespace?.StartsWith("CodeSnifferDog.Server.Services.Projects.Sidebar", StringComparison.Ordinal) == true,
                $"{type.Name} should stay under the Projects.Sidebar namespace.");
            Assert.IsFalse(
                type.Name.StartsWith("ProjectSidebar", StringComparison.Ordinal) ||
                type.Name.StartsWith("IProjectSidebar", StringComparison.Ordinal),
                $"{type.Name} should rely on its folder/namespace for project sidebar context.");
        }
    }

    [TestMethod]
    public void ProjectReportsServices_UseLocalRoleNames()
    {
        Type[] projectReportsTypes =
        [
            typeof(ReportsServiceInterface),
            typeof(ReportsService),
            typeof(ReportRuleReportDraft),
            typeof(ReportQueryServiceInterface),
            typeof(ReportQueryService),
            typeof(ReportQueryService.QueryRow),
            typeof(ReportProjectionMapperInterface),
            typeof(ReportProjectionMapper),
            typeof(ReportProjectProjection),
            typeof(ReportRuleReportProjection),
            typeof(ReportExportServiceInterface),
            typeof(ReportExportService),
            typeof(ReportExportFile),
        ];

        foreach (Type type in projectReportsTypes)
        {
            Assert.IsTrue(
                type.Namespace?.StartsWith("CodeSnifferDog.Server.Services.ProjectReports", StringComparison.Ordinal) == true ||
                type.DeclaringType?.Namespace?.StartsWith("CodeSnifferDog.Server.Services.ProjectReports", StringComparison.Ordinal) == true,
                $"{type.Name} should stay under the ProjectReports service namespace.");
            Assert.IsFalse(
                type.Name.StartsWith("ProjectReport", StringComparison.Ordinal) ||
                type.Name.StartsWith("IProjectReport", StringComparison.Ordinal) ||
                type.Name.StartsWith("ProjectRuleReport", StringComparison.Ordinal),
                $"{type.Name} should rely on its folder/namespace for project report context.");
        }
    }

    [TestMethod]
    public void ProjectIntakeSubServices_UseLocalRoleNames()
    {
        Type[] projectIntakeTypes =
        [
            typeof(CodeSnifferDog.Server.Services.ProjectIntake.Upload.IUploadService),
            typeof(CodeSnifferDog.Server.Services.ProjectIntake.Upload.UploadService),
            typeof(CodeSnifferDog.Server.Services.ProjectIntake.Upload.Artifact),
            typeof(CodeSnifferDog.Server.Services.ProjectIntake.Queue.IQueueService),
            typeof(CodeSnifferDog.Server.Services.ProjectIntake.Queue.QueueService),
            typeof(CodeSnifferDog.Server.Services.ProjectIntake.Queue.Request),
            typeof(CodeSnifferDog.Server.Services.ProjectIntake.Deletion.IDeletionService),
            typeof(CodeSnifferDog.Server.Services.ProjectIntake.Deletion.DeletionService),
        ];

        foreach (Type type in projectIntakeTypes)
        {
            Assert.IsTrue(
                type.Namespace?.StartsWith("CodeSnifferDog.Server.Services.ProjectIntake", StringComparison.Ordinal) == true,
                $"{type.Name} should stay under the ProjectIntake service namespace.");
            Assert.IsFalse(
                type.Name.StartsWith("ProjectUpload", StringComparison.Ordinal) ||
                type.Name.StartsWith("IProjectUpload", StringComparison.Ordinal) ||
                type.Name.StartsWith("ProjectQueue", StringComparison.Ordinal) ||
                type.Name.StartsWith("IProjectQueue", StringComparison.Ordinal) ||
                type.Name.StartsWith("ProjectDeletion", StringComparison.Ordinal) ||
                type.Name.StartsWith("IProjectDeletion", StringComparison.Ordinal),
                $"{type.Name} should rely on its folder/namespace for project intake context.");
        }
    }

    private static ServiceCollection CreateServices()
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddSignalR();
        services.AddCodeSnifferDogServerServices(
            CreateConfiguration(),
            options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        return services;
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CodeSnifferDogServer"] = "Server=(localdb)\\MSSQLLocalDB;Database=CodeSnifferDogTests;Trusted_Connection=True;",
                ["ProjectExecution:MaxConcurrentWorkers"] = "1",
                ["ProjectExecution:QueuePollingIntervalSeconds"] = "1",
                ["InferenceProvider:Provider"] = "OpenAICompatible",
                ["InferenceProvider:OpenAICompatible:Endpoint"] = "http://127.0.0.1:11434/v1",
                ["InferenceProvider:OpenAICompatible:ModelId"] = "test-model",
            })
            .Build();

    private static bool IsDbContextDependency(Type type) =>
        type == typeof(CodeSnifferDogServerDbContext) ||
        (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(IDbContextFactory<>) &&
            type.GetGenericArguments()[0] == typeof(CodeSnifferDogServerDbContext));

    private static bool ContainsSharedDto(Type type)
    {
        if (IsSharedDto(type))
            return true;

        if (type.IsGenericType)
            return type.GetGenericArguments().Any(ContainsSharedDto);

        return false;
    }

    private static bool IsSharedDto(Type type) =>
        type.Name.EndsWith("Dto", StringComparison.Ordinal) &&
        type.Namespace?.StartsWith("CodeSnifferDog.Server.Shared", StringComparison.Ordinal) == true;
}
