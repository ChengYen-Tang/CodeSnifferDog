using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using AnalysisRuleDefinition = CodeSnifferDog.Server.Services.ProjectExecution.Analysis.RuleDefinition;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ReviewAnalysisExecutorTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task AnalyzeAsync_PassesRulesAndExecutionOptionsToWorkerFactory_AndReturnsWorkerResult()
    {
        AnalysisResult expectedResult = CreateAnalysisResult();
        TestWorker worker = new(expectedResult);
        TestWorkerFactory workerFactory = new(worker)
        {
            PublishAgentCreatedEvent = true,
        };
        TestEventSubscriberFactory subscriberFactory = new();
        using ServiceProvider services = CreateServices(workerFactory, CreateOptions(), subscriberFactory);
        ReviewAnalysisExecutor executor = CreateExecutor(services);
        ProjectAnalysisContext context = CreateContext();
        IReadOnlyList<AnalysisRuleDefinition> rules = CreateRules();

        AnalysisResult result = await executor.AnalyzeAsync(context, rules, TestContext.CancellationToken);

        Assert.AreSame(expectedResult, result);
        Assert.AreSame(rules, workerFactory.Rules);
        Assert.AreEqual(context.RepositoryRootPath, workerFactory.RepositoryRootPath);
        Assert.AreEqual(3, workerFactory.ExecutionOptions!.MaxParallelAgents);
        Assert.AreEqual(64_000L, workerFactory.ExecutionOptions.ModelContextWindowTokens);
        Assert.AreEqual(CompactionMode.ContextCollapse, workerFactory.ExecutionOptions.ContextCompactionMode);
        Assert.AreSame(NoOpChatClient.Instance, workerFactory.ChatClient);
        Assert.IsTrue(worker.WasDisposed);
        Assert.AreEqual(context.ProjectId, subscriberFactory.ProjectIds.Single());
        Assert.AreEqual(2, subscriberFactory.Handlers.Single().HandledCount);
    }

    [TestMethod]
    public async Task AnalyzeAsync_WorkerThrows_DisposesWorkerAndFlushesAgentStatusEvents()
    {
        Guid projectId = Guid.NewGuid();
        TestWorker worker = new(CreateAnalysisResult())
        {
            Exception = new InvalidOperationException("worker failed."),
        };
        TestWorkerFactory workerFactory = new(worker)
        {
            PublishAgentCreatedEvent = true,
        };
        using ServiceProvider services = CreateServices(workerFactory, CreateOptions());
        ReviewAnalysisExecutor executor = CreateExecutor(services);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            executor.AnalyzeAsync(
                new ProjectAnalysisContext
                {
                    ProjectId = projectId,
                    RepositoryRootPath = TestRepositoryPaths.RootPath,
                },
                CreateRules(),
                TestContext.CancellationToken));

        Assert.AreEqual("worker failed.", exception.Message);
        Assert.IsTrue(worker.WasDisposed);
        await AssertPersistedAgentAsync(services, projectId);
    }

    [TestMethod]
    public async Task AnalyzeAsync_WorkerFactoryThrows_FlushesAgentStatusEvents()
    {
        Guid projectId = Guid.NewGuid();
        TestWorker worker = new(CreateAnalysisResult());
        TestWorkerFactory workerFactory = new(worker)
        {
            PublishAgentCreatedEvent = true,
            Exception = new InvalidOperationException("worker factory failed."),
        };
        using ServiceProvider services = CreateServices(workerFactory, CreateOptions());
        ReviewAnalysisExecutor executor = CreateExecutor(services);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            executor.AnalyzeAsync(
                new ProjectAnalysisContext
                {
                    ProjectId = projectId,
                    RepositoryRootPath = TestRepositoryPaths.RootPath,
                },
                CreateRules(),
                TestContext.CancellationToken));

        Assert.AreEqual("worker factory failed.", exception.Message);
        Assert.IsFalse(worker.WasDisposed);
        await AssertPersistedAgentAsync(services, projectId);
    }

    private static ReviewAnalysisExecutor CreateExecutor(ServiceProvider services) =>
        new(
            services.GetRequiredService<IProjectChatClientProvider>(),
            services.GetRequiredService<IWorkerFactory>(),
            services.GetRequiredService<IEventSubscriberFactory>(),
            services.GetRequiredService<IOptions<Settings>>());

    private static ServiceProvider CreateServices(
        IWorkerFactory workerFactory,
        ExecutionOptions executionOptions,
        IEventSubscriberFactory? subscriberFactory = null)
    {
        InMemoryDatabaseRoot databaseRoot = new();
        string databaseName = Guid.NewGuid().ToString("N");
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddSingleton<IProjectChatClientProvider, ReadyChatClientProvider>();
        services.AddSingleton(workerFactory);
        services.AddSingleton<ILiveUpdateNotifier, NoOpLiveUpdateNotifier>();
        services.AddSingleton<IProjectStatusMapper, ProjectStatusMapper>();
        services.AddSingleton<IProjectionMapper, ProjectionMapper>();
        services.AddSingleton<IEventSubscriberFactory>(serviceProvider =>
            subscriberFactory
            ?? new EventSubscriberFactory(
                new RuntimeFactory(
                    new RuntimeComponentsFactory(
                        serviceProvider.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>(),
                        serviceProvider.GetRequiredService<ILiveUpdateNotifier>(),
                        serviceProvider.GetRequiredService<IProjectionMapper>(),
                        new TimelinePersistenceService()))));
        services.AddSingleton(Options.Create(new Settings
        {
            ExecutionOptions = executionOptions,
        }));
        return services.BuildServiceProvider();
    }

    private static ExecutionOptions CreateOptions() =>
        new()
        {
            MaxParallelAgents = 3,
            ModelContextWindowTokens = 64_000,
            ContextCompactionMode = CompactionMode.ContextCollapse,
            AgentRunTimeoutSeconds = 42,
            MaxConsecutiveAgentRunFailures = 7,
        };

    private static ProjectAnalysisContext CreateContext() =>
        new()
        {
            ProjectId = Guid.NewGuid(),
            RepositoryRootPath = TestRepositoryPaths.RootPath,
        };

    private static IReadOnlyList<AnalysisRuleDefinition> CreateRules() =>
    [
        new()
        {
            RuleKey = "rule-a",
            RuleName = "Rule A",
            RuleMarkdown = "- Rule A",
        },
        new()
        {
            RuleKey = "rule-b",
            RuleName = "Rule B",
            RuleMarkdown = "- Rule B",
        },
    ];

    private static AnalysisResult CreateAnalysisResult() =>
        new()
        {
            PreparationSucceeded = true,
            ReviewStageSucceeded = true,
            HasAnyFindings = false,
            AllRuleFlowsSucceeded = true,
            ExecutionErrors = [],
            RuleReports = [],
        };

    private static async Task AssertPersistedAgentAsync(ServiceProvider services, Guid projectId)
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync();

        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(group => group.ProjectId == projectId && group.RuntimeKey == "group-a");
        ProjectAgentRecord agent = await dbContext.ProjectAgents
            .SingleAsync(agent => agent.ProjectAgentGroupId == group.Id && agent.RuntimeKey == "agent-a");

        Assert.AreEqual("Agent A", agent.DisplayName);
        Assert.AreEqual("System prompt", agent.SystemPrompt);
    }

    private sealed class TestWorkerFactory(TestWorker worker) : IWorkerFactory
    {
        private readonly TestWorker _worker = worker;

        public bool PublishAgentCreatedEvent { get; init; }

        public Exception? Exception { get; init; }

        public IChatClient? ChatClient { get; private set; }

        public string? RepositoryRootPath { get; private set; }

        public IReadOnlyList<AnalysisRuleDefinition>? Rules { get; private set; }

        public ExecutionOptions? ExecutionOptions { get; private set; }

        public IWorker CreateWorker(
            IChatClient chatClient,
            string repositoryRootPath,
            IReadOnlyList<AnalysisRuleDefinition> rules,
            ExecutionOptions executionOptions,
            IAgentEventBus agentEventBus)
        {
            ChatClient = chatClient;
            RepositoryRootPath = repositoryRootPath;
            Rules = rules;
            ExecutionOptions = executionOptions;

            if (PublishAgentCreatedEvent)
                PublishAgentCreatedEventAsync(agentEventBus, CancellationToken.None).GetAwaiter().GetResult();

            if (Exception is not null)
                throw Exception;

            return _worker;
        }

        private static async Task PublishAgentCreatedEventAsync(
            IAgentEventBus agentEventBus,
            CancellationToken cancellationToken)
        {
            await agentEventBus.PublishGroupCreatedAsync("group-a", "Group A", cancellationToken);
            IAgentEventScope scope = agentEventBus.CreateScope("group-a", "agent-a");
            await scope.PublishCreatedAsync("Agent A", "System prompt", "Waiting", cancellationToken);
        }
    }

    private sealed class TestWorker(AnalysisResult result) : IWorker
    {
        private readonly AnalysisResult _result = result;

        public Func<CancellationToken, Task>? OnAnalyzeAsync { get; set; }

        public Exception? Exception { get; init; }

        public bool WasDisposed { get; private set; }

        public async Task<AnalysisResult> AnalyzeDetailedAsync(CancellationToken cancellationToken = default)
        {
            if (OnAnalyzeAsync is not null)
                await OnAnalyzeAsync(cancellationToken);

            if (Exception is not null)
                throw Exception;

            return _result;
        }

        public ValueTask DisposeAsync()
        {
            WasDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ReadyChatClientProvider : IProjectChatClientProvider
    {
        public bool IsReady => true;

        public IChatClient CreateChatClient() => NoOpChatClient.Instance;
    }

    private sealed class NoOpLiveUpdateNotifier : ILiveUpdateNotifier
    {
        public Task NotifyAsync(LiveUpdateDto update, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TestEventSubscriberFactory : IEventSubscriberFactory
    {
        public List<Guid> ProjectIds { get; } = [];

        public List<TrackingEventHandler> Handlers { get; } = [];

        public EventSubscriber Create(Guid projectId, IObservable<StatusEvent> events)
        {
            ProjectIds.Add(projectId);
            TrackingEventHandler handler = new();
            Handlers.Add(handler);
            return new EventSubscriber(handler, events);
        }
    }

    private sealed class TrackingEventHandler : IEventHandler
    {
        public int HandledCount { get; private set; }

        public Task HandleAsync(StatusEvent agentEvent, CancellationToken cancellationToken)
        {
            HandledCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpChatClient : IChatClient
    {
        public static NoOpChatClient Instance { get; } = new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty)));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
