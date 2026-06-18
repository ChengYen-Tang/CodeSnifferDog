using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Server.Services.ProjectExecution;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ProjectReviewAgentTeamWorkerFactoryTests
{
    [TestMethod]
    public async Task CreateWorker_MapsRulesAndExecutionOptions_ToReviewAgentTeamWorker()
    {
        CapturedWorkerFactory capturedWorkerFactory = new();
        ProjectReviewAgentTeamWorkerFactory factory = new(
            NullLoggerFactory.Instance,
            new ServiceCollection().BuildServiceProvider(),
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
        Assert.AreEqual(@"Z:\GitHub\CodeSnifferDog", capturedWorkerFactory.RepositoryRootPath);
        Assert.HasCount(2, capturedWorkerFactory.RuleDefinitions!);
        Assert.AreEqual("rule-a", capturedWorkerFactory.RuleDefinitions![0].RuleKey);
        Assert.AreEqual("- Rule A", capturedWorkerFactory.RuleDefinitions[0].RuleMarkdown);
        Assert.AreEqual("rule-b", capturedWorkerFactory.RuleDefinitions[1].RuleKey);
        Assert.AreEqual("- Rule B", capturedWorkerFactory.RuleDefinitions[1].RuleMarkdown);
        Assert.AreEqual(4, capturedWorkerFactory.ExecutionOptions!.MaxParallelAgents);
        Assert.AreEqual(32_000L, capturedWorkerFactory.ExecutionOptions.ModelContextWindowTokens);
        Assert.AreEqual(OperationalContextCompactionMode.ReactiveOnly, capturedWorkerFactory.ExecutionOptions.ContextCompactionMode);
        Assert.IsNotNull(capturedWorkerFactory.Dependencies!.RuleReportIssueStore);
        Assert.AreSame(NoOpAgentEventBus.Instance, capturedWorkerFactory.Dependencies.AgentEventBus);
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
