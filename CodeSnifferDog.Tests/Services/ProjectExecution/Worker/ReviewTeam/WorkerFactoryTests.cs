using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using AnalysisRuleDefinition = CodeSnifferDog.Server.Services.ProjectExecution.Analysis.RuleDefinition;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using FluentResults;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;
using Dependencies = CodeSnifferDog.Models.ReviewAgentTeam.Runtime.Dependencies;
using TeamExecutionOptions = CodeSnifferDog.Models.ReviewAgentTeam.Runtime.ExecutionOptions;
using TeamFactory = CodeSnifferDog.Modules.ReviewAgentTeam.Runtime.Factory;
using TeamRuleDefinition = CodeSnifferDog.Models.ReviewAgentTeam.Agents.RuleDefinition;
using TeamWorker = CodeSnifferDog.Modules.ReviewAgentTeam.Runtime.Worker;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Tests.Services.ProjectExecution.Worker.ReviewTeam;

[TestClass]
public sealed class WorkerFactoryTests
{
    [TestMethod]
    public async Task CreateWorker_MapsRulesAndExecutionOptions_AndUsesDependenciesFactory()
    {
        CapturedDependenciesFactory dependenciesFactory = new();
        CapturedWorkerFactory capturedWorkerFactory = new();
        WorkerFactory factory = new(
            dependenciesFactory,
            capturedWorkerFactory.CreateWorker);
        IReadOnlyList<AnalysisRuleDefinition> rules = CreateRules();
        ExecutionOptions executionOptions = new()
        {
            MaxParallelAgents = 4,
            ModelContextWindowTokens = 32_000,
            ContextCompactionMode = CompactionMode.ReactiveOnly,
            AgentRunTimeoutSeconds = 11,
            MaxConsecutiveAgentRunFailures = 6,
        };

        await using IWorker worker = factory.CreateWorker(
            NoOpChatClient.Instance,
            TestRepositoryPaths.RootPath,
            rules,
            executionOptions,
            NoOpAgentEventBus.Instance);

        Assert.IsNotNull(worker);
        Assert.AreSame(NoOpChatClient.Instance, dependenciesFactory.ChatClient);
        Assert.AreSame(executionOptions, dependenciesFactory.ExecutionOptions);
        Assert.AreSame(NoOpAgentEventBus.Instance, dependenciesFactory.AgentEventBus);
        Assert.AreSame(dependenciesFactory.Dependencies, capturedWorkerFactory.Dependencies);
        Assert.AreEqual(TestRepositoryPaths.RootPath, capturedWorkerFactory.RepositoryRootPath);
        Assert.HasCount(2, capturedWorkerFactory.RuleDefinitions!);
        Assert.AreEqual("rule-a", capturedWorkerFactory.RuleDefinitions![0].RuleKey);
        Assert.AreEqual("- Rule A", capturedWorkerFactory.RuleDefinitions[0].RuleMarkdown);
        Assert.AreEqual("rule-b", capturedWorkerFactory.RuleDefinitions[1].RuleKey);
        Assert.AreEqual("- Rule B", capturedWorkerFactory.RuleDefinitions[1].RuleMarkdown);
        Assert.AreEqual(4, capturedWorkerFactory.ExecutionOptions!.MaxParallelAgents);
        Assert.AreEqual(32_000L, capturedWorkerFactory.ExecutionOptions.ModelContextWindowTokens);
        Assert.AreEqual(CompactionMode.ReactiveOnly, capturedWorkerFactory.ExecutionOptions.ContextCompactionMode);
    }

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

    private sealed class CapturedDependenciesFactory : IDependenciesFactory
    {
        public Dependencies Dependencies { get; } = new()
        {
            ScanWorkflowRunner = (_, _) => Task.FromResult(Result.Fail<Models.Scan.ScanWorkflowResult>("Not used.")),
            ProjectPlanWorkflowRunner = (_, _, _) => Task.FromResult(Result.Fail<Models.ProjectPlan.WorkflowResult>("Not used.")),
            RuleFlowWorkflowRunner = (_, _, _, _, _) => Task.FromResult(Result.Fail<Models.RuleFlow.WorkflowResult>("Not used.")),
            RuleReportIssueStore = new InMemoryIssueStore(),
            AgentEventBus = NoOpAgentEventBus.Instance,
        };

        public IChatClient? ChatClient { get; private set; }

        public ExecutionOptions? ExecutionOptions { get; private set; }

        public IAgentEventBus? AgentEventBus { get; private set; }

        public Dependencies CreateDependencies(
            IChatClient chatClient,
            ExecutionOptions executionOptions,
            IAgentEventBus agentEventBus)
        {
            ChatClient = chatClient;
            ExecutionOptions = executionOptions;
            AgentEventBus = agentEventBus;
            return Dependencies;
        }
    }

    private sealed class CapturedWorkerFactory
    {
        public Dependencies? Dependencies { get; private set; }

        public string? RepositoryRootPath { get; private set; }

        public IReadOnlyList<TeamRuleDefinition>? RuleDefinitions { get; private set; }

        public TeamExecutionOptions? ExecutionOptions { get; private set; }

        public TeamWorker CreateWorker(
            Dependencies dependencies,
            string repositoryRootPath,
            IReadOnlyList<TeamRuleDefinition> ruleDefinitions,
            TeamExecutionOptions executionOptions)
        {
            Dependencies = dependencies;
            RepositoryRootPath = repositoryRootPath;
            RuleDefinitions = ruleDefinitions;
            ExecutionOptions = executionOptions;

            return new TeamFactory(dependencies).CreateWorker(
                repositoryRootPath,
                ruleDefinitions,
                executionOptions);
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
