using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution;
using FluentResults;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ProjectReviewAgentTeamDependenciesFactoryTests
{
    [TestMethod]
    public void CreateDependencies_UsesWorkflowRunnersSharedReportStoreAndEventBus()
    {
        ProjectReviewAgentCompactionOptionsFactory compactionOptionsFactory = new();
        CapturingWorkflowRunnerFactory workflowRunnerFactory = new();
        ProjectReviewAgentTeamDependenciesFactory dependenciesFactory = new(
            compactionOptionsFactory,
            workflowRunnerFactory);
        ExecutionOptions executionOptions = new()
        {
            MaxParallelAgents = 2,
            ModelContextWindowTokens = 16_000,
            ContextCompactionMode = OperationalContextCompactionMode.ContextCollapse,
        };

        ReviewAgentTeamDependencies dependencies = dependenciesFactory.CreateDependencies(
            NoOpChatClient.Instance,
            executionOptions,
            NoOpAgentEventBus.Instance);

        Assert.IsNotNull(dependencies.ScanWorkflowRunner);
        Assert.IsNotNull(dependencies.ProjectPlanWorkflowRunner);
        Assert.IsNotNull(dependencies.RuleFlowWorkflowRunner);
        Assert.IsNotNull(dependencies.RuleReportIssueStore);
        Assert.AreSame(NoOpAgentEventBus.Instance, dependencies.AgentEventBus);
        Assert.AreSame(NoOpChatClient.Instance, workflowRunnerFactory.ChatClient);
        Assert.AreSame(executionOptions, workflowRunnerFactory.ExecutionOptions);
        Assert.AreSame(NoOpAgentEventBus.Instance, workflowRunnerFactory.AgentEventBus);
        Assert.AreSame(dependencies.RuleReportIssueStore, workflowRunnerFactory.RuleReportIssueStore);
        Assert.IsInstanceOfType<InMemoryRuleReviewIssueStore>(workflowRunnerFactory.RuleReviewIssueStore);
        Assert.AreEqual(16_000L, workflowRunnerFactory.CompactionSettings!.Scan.ModelContextWindowTokens);
        Assert.AreEqual(OperationalContextCompactionMode.ContextCollapse, workflowRunnerFactory.CompactionSettings.Scan.Mode);
    }

    private sealed class CapturingWorkflowRunnerFactory : IProjectReviewWorkflowRunnerFactory
    {
        public IChatClient? ChatClient { get; private set; }

        public ExecutionOptions? ExecutionOptions { get; private set; }

        public ProjectReviewAgentCompactionSettings? CompactionSettings { get; private set; }

        public IRuleReviewIssueStore? RuleReviewIssueStore { get; private set; }

        public IRuleReportIssueStore? RuleReportIssueStore { get; private set; }

        public IAgentEventBus? AgentEventBus { get; private set; }

        public ProjectReviewWorkflowRunners CreateRunners(
            IChatClient chatClient,
            ExecutionOptions executionOptions,
            ProjectReviewAgentCompactionSettings compactionSettings,
            IRuleReviewIssueStore ruleReviewIssueStore,
            IRuleReportIssueStore ruleReportIssueStore,
            IAgentEventBus agentEventBus)
        {
            ChatClient = chatClient;
            ExecutionOptions = executionOptions;
            CompactionSettings = compactionSettings;
            RuleReviewIssueStore = ruleReviewIssueStore;
            RuleReportIssueStore = ruleReportIssueStore;
            AgentEventBus = agentEventBus;

            return new ProjectReviewWorkflowRunners
            {
                ScanWorkflowRunner = (_, _) => Task.FromResult(Result.Fail<Models.Scan.ScanWorkflowResult>("Not used.")),
                ProjectPlanWorkflowRunner = (_, _, _) => Task.FromResult(Result.Fail<Models.ProjectPlan.ProjectPlanWorkflowResult>("Not used.")),
                RuleFlowWorkflowRunner = (_, _, _, _, _) => Task.FromResult(Result.Fail<Models.RuleFlow.RuleFlowWorkflowResult>("Not used.")),
            };
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
