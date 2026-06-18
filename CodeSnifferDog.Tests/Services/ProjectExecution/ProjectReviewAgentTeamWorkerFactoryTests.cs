using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using FluentResults;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ProjectReviewAgentTeamWorkerFactoryTests
{
    [TestMethod]
    public async Task CreateWorker_MapsRulesAndExecutionOptions_AndUsesDependenciesFactory()
    {
        CapturedDependenciesFactory dependenciesFactory = new();
        CapturedWorkerFactory capturedWorkerFactory = new();
        ProjectReviewAgentTeamWorkerFactory factory = new(
            dependenciesFactory,
            capturedWorkerFactory.CreateWorker);
        IReadOnlyList<ProjectExecutionRuleDefinition> rules = CreateRules();
        ExecutionOptions executionOptions = new()
        {
            MaxParallelAgents = 4,
            ModelContextWindowTokens = 32_000,
            ContextCompactionMode = OperationalContextCompactionMode.ReactiveOnly,
            AgentRunTimeoutSeconds = 11,
            MaxConsecutiveAgentRunFailures = 6,
        };

        await using IProjectReviewAgentTeamWorker worker = factory.CreateWorker(
            NoOpChatClient.Instance,
            @"Z:\GitHub\CodeSnifferDog",
            rules,
            executionOptions,
            NoOpAgentEventBus.Instance);

        Assert.IsNotNull(worker);
        Assert.AreSame(NoOpChatClient.Instance, dependenciesFactory.ChatClient);
        Assert.AreSame(executionOptions, dependenciesFactory.ExecutionOptions);
        Assert.AreSame(NoOpAgentEventBus.Instance, dependenciesFactory.AgentEventBus);
        Assert.AreSame(dependenciesFactory.Dependencies, capturedWorkerFactory.Dependencies);
        Assert.AreEqual(@"Z:\GitHub\CodeSnifferDog", capturedWorkerFactory.RepositoryRootPath);
        Assert.HasCount(2, capturedWorkerFactory.RuleDefinitions!);
        Assert.AreEqual("rule-a", capturedWorkerFactory.RuleDefinitions![0].RuleKey);
        Assert.AreEqual("- Rule A", capturedWorkerFactory.RuleDefinitions[0].RuleMarkdown);
        Assert.AreEqual("rule-b", capturedWorkerFactory.RuleDefinitions[1].RuleKey);
        Assert.AreEqual("- Rule B", capturedWorkerFactory.RuleDefinitions[1].RuleMarkdown);
        Assert.AreEqual(4, capturedWorkerFactory.ExecutionOptions!.MaxParallelAgents);
        Assert.AreEqual(32_000L, capturedWorkerFactory.ExecutionOptions.ModelContextWindowTokens);
        Assert.AreEqual(OperationalContextCompactionMode.ReactiveOnly, capturedWorkerFactory.ExecutionOptions.ContextCompactionMode);
    }

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

    private sealed class CapturedDependenciesFactory : IProjectReviewAgentTeamDependenciesFactory
    {
        public ReviewAgentTeamDependencies Dependencies { get; } = new()
        {
            ScanWorkflowRunner = (_, _) => Task.FromResult(Result.Fail<Models.Scan.ScanWorkflowResult>("Not used.")),
            ProjectPlanWorkflowRunner = (_, _, _) => Task.FromResult(Result.Fail<Models.ProjectPlan.ProjectPlanWorkflowResult>("Not used.")),
            RuleFlowWorkflowRunner = (_, _, _, _, _) => Task.FromResult(Result.Fail<Models.RuleFlow.RuleFlowWorkflowResult>("Not used.")),
            RuleReportIssueStore = new InMemoryRuleReportIssueStore(),
            AgentEventBus = NoOpAgentEventBus.Instance,
        };

        public IChatClient? ChatClient { get; private set; }

        public ExecutionOptions? ExecutionOptions { get; private set; }

        public IAgentEventBus? AgentEventBus { get; private set; }

        public ReviewAgentTeamDependencies CreateDependencies(
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
        public ReviewAgentTeamDependencies? Dependencies { get; private set; }

        public string? RepositoryRootPath { get; private set; }

        public IReadOnlyList<ReviewAgentRuleDefinition>? RuleDefinitions { get; private set; }

        public ReviewAgentTeamExecutionOptions? ExecutionOptions { get; private set; }

        public ReviewAgentTeamWorker CreateWorker(
            ReviewAgentTeamDependencies dependencies,
            string repositoryRootPath,
            IReadOnlyList<ReviewAgentRuleDefinition> ruleDefinitions,
            ReviewAgentTeamExecutionOptions executionOptions)
        {
            Dependencies = dependencies;
            RepositoryRootPath = repositoryRootPath;
            RuleDefinitions = ruleDefinitions;
            ExecutionOptions = executionOptions;

            return new ReviewAgentTeamFactory(dependencies).CreateWorker(
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
