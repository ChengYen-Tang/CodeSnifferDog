using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Status;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ProjectReviewAnalysisExecutorTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task AnalyzeAsync_PassesRulesAndExecutionOptionsToWorkerFactory_AndReturnsWorkerResult()
    {
        ReviewAgentTeamAnalysisResult expectedResult = CreateAnalysisResult();
        TestWorker worker = new(expectedResult);
        TestWorkerFactory workerFactory = new(worker)
        {
            PublishAgentCreatedEvent = true,
        };
        TestAgentStatusEventSubscriberFactory subscriberFactory = new();
        using ServiceProvider services = CreateServices(workerFactory, CreateOptions(), subscriberFactory);
        ProjectReviewAnalysisExecutor executor = CreateExecutor(services);
        ProjectAnalysisContext context = CreateContext();
        IReadOnlyList<ProjectExecutionRuleDefinition> rules = CreateRules();

        ReviewAgentTeamAnalysisResult result = await executor.AnalyzeAsync(context, rules, TestContext.CancellationToken);

        Assert.AreSame(expectedResult, result);
        Assert.AreSame(rules, workerFactory.Rules);
        Assert.AreEqual(context.RepositoryRootPath, workerFactory.RepositoryRootPath);
        Assert.AreEqual(3, workerFactory.ExecutionOptions!.MaxParallelAgents);
        Assert.AreEqual(64_000L, workerFactory.ExecutionOptions.ModelContextWindowTokens);
        Assert.AreEqual(OperationalContextCompactionMode.ContextCollapse, workerFactory.ExecutionOptions.ContextCompactionMode);
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
        ProjectReviewAnalysisExecutor executor = CreateExecutor(services);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            executor.AnalyzeAsync(
                new ProjectAnalysisContext
                {
                    ProjectId = projectId,
                    RepositoryRootPath = @"Z:\GitHub\CodeSnifferDog",
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
        ProjectReviewAnalysisExecutor executor = CreateExecutor(services);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            executor.AnalyzeAsync(
                new ProjectAnalysisContext
                {
                    ProjectId = projectId,
                    RepositoryRootPath = @"Z:\GitHub\CodeSnifferDog",
                },
                CreateRules(),
                TestContext.CancellationToken));

        Assert.AreEqual("worker factory failed.", exception.Message);
        Assert.IsFalse(worker.WasDisposed);
        await AssertPersistedAgentAsync(services, projectId);
    }

    private static ProjectReviewAnalysisExecutor CreateExecutor(ServiceProvider services) =>
        new(
            services.GetRequiredService<IProjectChatClientProvider>(),
            services.GetRequiredService<IProjectReviewAgentTeamWorkerFactory>(),
            services.GetRequiredService<IAgentStatusEventSubscriberFactory>(),
            services.GetRequiredService<IOptions<ProjectExecutionOptions>>());

    private static ServiceProvider CreateServices(
        IProjectReviewAgentTeamWorkerFactory workerFactory,
        ExecutionOptions executionOptions,
        IAgentStatusEventSubscriberFactory? subscriberFactory = null)
    {
        InMemoryDatabaseRoot databaseRoot = new();
        string databaseName = Guid.NewGuid().ToString("N");
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddSingleton<IProjectChatClientProvider, ReadyChatClientProvider>();
        services.AddSingleton(workerFactory);
        services.AddSingleton<IProjectAgentStatusLiveUpdateNotifier, NoOpProjectAgentStatusLiveUpdateNotifier>();
        services.AddSingleton<IAgentStatusProjectionMapper, AgentStatusProjectionMapper>();
        services.AddSingleton<IAgentStatusEventSubscriberFactory>(serviceProvider =>
            subscriberFactory
            ?? new AgentStatusEventSubscriberFactory(
                new AgentStatusRuntimeFactory(
                    serviceProvider.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>(),
                    serviceProvider.GetRequiredService<IProjectAgentStatusLiveUpdateNotifier>(),
                    serviceProvider.GetRequiredService<IAgentStatusProjectionMapper>())));
        services.AddSingleton(Options.Create(new ProjectExecutionOptions
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
            ContextCompactionMode = OperationalContextCompactionMode.ContextCollapse,
            AgentRunTimeoutSeconds = 42,
            MaxConsecutiveAgentRunFailures = 7,
        };

    private static ProjectAnalysisContext CreateContext() =>
        new()
        {
            ProjectId = Guid.NewGuid(),
            RepositoryRootPath = @"Z:\GitHub\CodeSnifferDog",
        };

    private static IReadOnlyList<ProjectExecutionRuleDefinition> CreateRules() =>
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

    private static ReviewAgentTeamAnalysisResult CreateAnalysisResult() =>
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

    private sealed class TestWorkerFactory(TestWorker worker) : IProjectReviewAgentTeamWorkerFactory
    {
        private readonly TestWorker _worker = worker;

        public bool PublishAgentCreatedEvent { get; init; }

        public Exception? Exception { get; init; }

        public IChatClient? ChatClient { get; private set; }

        public string? RepositoryRootPath { get; private set; }

        public IReadOnlyList<ProjectExecutionRuleDefinition>? Rules { get; private set; }

        public ExecutionOptions? ExecutionOptions { get; private set; }

        public IProjectReviewAgentTeamWorker CreateWorker(
            IChatClient chatClient,
            string repositoryRootPath,
            IReadOnlyList<ProjectExecutionRuleDefinition> rules,
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

    private sealed class TestWorker(ReviewAgentTeamAnalysisResult result) : IProjectReviewAgentTeamWorker
    {
        private readonly ReviewAgentTeamAnalysisResult _result = result;

        public Func<CancellationToken, Task>? OnAnalyzeAsync { get; set; }

        public Exception? Exception { get; init; }

        public bool WasDisposed { get; private set; }

        public async Task<ReviewAgentTeamAnalysisResult> AnalyzeDetailedAsync(CancellationToken cancellationToken = default)
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

    private sealed class NoOpProjectAgentStatusLiveUpdateNotifier : IProjectAgentStatusLiveUpdateNotifier
    {
        public Task NotifyAsync(ProjectAgentLiveUpdateDto update, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TestAgentStatusEventSubscriberFactory : IAgentStatusEventSubscriberFactory
    {
        public List<Guid> ProjectIds { get; } = [];

        public List<TrackingAgentStatusEventHandler> Handlers { get; } = [];

        public ProjectAgentStatusEventSubscriber Create(Guid projectId, IObservable<AgentStatusEvent> events)
        {
            ProjectIds.Add(projectId);
            TrackingAgentStatusEventHandler handler = new();
            Handlers.Add(handler);
            return new ProjectAgentStatusEventSubscriber(handler, events);
        }
    }

    private sealed class TrackingAgentStatusEventHandler : IAgentStatusEventHandler
    {
        public int HandledCount { get; private set; }

        public Task HandleAsync(AgentStatusEvent agentEvent, CancellationToken cancellationToken)
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
