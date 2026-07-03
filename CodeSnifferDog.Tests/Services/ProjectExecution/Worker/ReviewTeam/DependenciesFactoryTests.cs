using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using FluentResults;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using Dependencies = CodeSnifferDog.Models.ReviewAgentTeam.Runtime.Dependencies;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using ReportIssueStore = CodeSnifferDog.Modules.Tools.Report.IIssueStore;
using ReportInMemoryIssueStore = CodeSnifferDog.Modules.Tools.Report.InMemoryIssueStore;
using ReviewIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.IIssueStore;
using ReviewInMemoryIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.InMemoryIssueStore;
using ProjectPlanWorkflowResult = CodeSnifferDog.Models.ProjectPlan.WorkflowResult;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Tests.Services.ProjectExecution.Worker.ReviewTeam;

[TestClass]
public sealed class DependenciesFactoryTests
{
    [TestMethod]
    public void CreateDependencies_UsesLocalStoresCompactionSettingsAndEventBus()
    {
        CapturingWorkflowRunnerFactory workflowRunnerFactory = new();
        OptionsFactory compactionOptionsFactory = new();
        DependenciesFactory factory = new(compactionOptionsFactory, workflowRunnerFactory);
        ExecutionOptions executionOptions = new()
        {
            ModelContextWindowTokens = 32_000,
            ContextCompactionMode = CompactionMode.ReactiveOnly,
        };
        FakeAgentEventBus agentEventBus = new();

        Dependencies dependencies = factory.CreateDependencies(
            NoOpChatClient.Instance,
            executionOptions,
            agentEventBus);

        Assert.AreSame(NoOpChatClient.Instance, workflowRunnerFactory.ChatClient);
        Assert.AreSame(executionOptions, workflowRunnerFactory.ExecutionOptions);
        Assert.AreSame(agentEventBus, workflowRunnerFactory.AgentEventBus);
        Assert.IsInstanceOfType<ReviewInMemoryIssueStore>(workflowRunnerFactory.RuleReviewIssueStore);
        Assert.IsInstanceOfType<ReportInMemoryIssueStore>(workflowRunnerFactory.RuleReportIssueStore);
        Assert.AreSame(workflowRunnerFactory.RuleReportIssueStore, dependencies.RuleReportIssueStore);
        Assert.AreSame(agentEventBus, dependencies.AgentEventBus);
        Assert.IsNotNull(dependencies.ScanWorkflowRunner);
        Assert.IsNotNull(dependencies.ProjectPlanWorkflowRunner);
        Assert.IsNotNull(dependencies.RuleFlowWorkflowRunner);
        Assert.AreEqual(32_000L, workflowRunnerFactory.CompactionSettings!.Scan.ModelContextWindowTokens);
        Assert.AreEqual(CompactionMode.ReactiveOnly, workflowRunnerFactory.CompactionSettings.Report.Mode);
    }

    private sealed class CapturingWorkflowRunnerFactory : IReviewRunnerFactory
    {
        public IChatClient? ChatClient { get; private set; }

        public ExecutionOptions? ExecutionOptions { get; private set; }

        public Settings? CompactionSettings { get; private set; }

        public ReviewIssueStore? RuleReviewIssueStore { get; private set; }

        public ReportIssueStore? RuleReportIssueStore { get; private set; }

        public IAgentEventBus? AgentEventBus { get; private set; }

        public ReviewRunners CreateRunners(
            IChatClient chatClient,
            ExecutionOptions executionOptions,
            Settings compactionSettings,
            ReviewIssueStore ruleReviewIssueStore,
            ReportIssueStore ruleReportIssueStore,
            IAgentEventBus agentEventBus)
        {
            ChatClient = chatClient;
            ExecutionOptions = executionOptions;
            CompactionSettings = compactionSettings;
            RuleReviewIssueStore = ruleReviewIssueStore;
            RuleReportIssueStore = ruleReportIssueStore;
            AgentEventBus = agentEventBus;

            return new ReviewRunners
            {
                ScanWorkflowRunner = (_, _) => Task.FromResult(Result.Fail<ScanWorkflowResult>("Not used.")),
                ProjectPlanWorkflowRunner = (_, _, _) => Task.FromResult(Result.Fail<ProjectPlanWorkflowResult>("Not used.")),
                RuleFlowWorkflowRunner = (_, _, _, _, _) => Task.FromResult(Result.Fail<RuleFlowWorkflowResult>("Not used.")),
            };
        }
    }

    private sealed class FakeAgentEventBus : IAgentEventBus
    {
        public IAgentEventScope CreateScope(string groupKey, string agentKey) =>
            new FakeAgentEventScope(groupKey, agentKey);

        public ValueTask PublishGroupCreatedAsync(
            string groupKey,
            string displayName,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class FakeAgentEventScope(string groupKey, string agentKey) : IAgentEventScope
    {
        public string GroupKey { get; } = groupKey;

        public string AgentKey { get; } = agentKey;

        public ValueTask PublishCreatedAsync(string displayName, string systemPrompt, string initialStatus, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishStatusChangedAsync(string status, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishUserMessageAsync(string message, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishAssistantMessageAsync(string message, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishToolCallStartedAsync(string toolCallId, string toolName, string? arguments, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishToolCallCompletedAsync(string toolCallId, string? result, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishCompactionAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishTranscriptClearedAsync(DateTimeOffset clearAfterUtc, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
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
