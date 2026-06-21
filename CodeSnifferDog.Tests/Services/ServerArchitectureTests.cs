using CodeSnifferDog.Server;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;
using CodeSnifferDog.Server.Services.ProjectReports.Queries;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Services.Projects.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

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
            typeof(ProjectAgentStatusSnapshotService),
            typeof(ProjectAgentStatusLiveBackfillService),
            typeof(ProjectSidebarSnapshotService),
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
            typeof(IProjectReportQueryService),
            typeof(IProjectAgentStatusSnapshotQueryService),
            typeof(IProjectAgentStatusBackfillQueryService),
            typeof(IProjectSidebarQueryService),
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
            // Built by AgentStatusRuntimeComponentsFactory for a single project runtime.
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence.IAgentStatusEventHandler),
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence.IAgentStatusPersistenceService),

            // Returned by ProjectReviewAgentTeamWorkerFactory; not resolved from DI directly.
            typeof(CodeSnifferDog.Server.Services.ProjectExecution.Worker.IProjectReviewAgentTeamWorker),
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
