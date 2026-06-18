using CodeSnifferDog.Agents.ProjectPlan;
using CodeSnifferDog.Agents.Report;
using CodeSnifferDog.Agents.RuleReview;
using CodeSnifferDog.Agents.Scan;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using FluentResults;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ProjectReviewWorkflowRunnerFactoryTests
{
    [TestMethod]
    public void CreateRunners_DelegatesSharedContextOptionsStoresAndEventBusToRunnerBuilders()
    {
        CapturedScanRunnerFactory capturedScan = new();
        CapturedProjectPlanRunnerFactory capturedProjectPlan = new();
        CapturedRuleFlowRunnerFactory capturedRuleFlow = new();
        ProjectReviewWorkflowRunnerFactory factory = new(
            capturedScan,
            capturedProjectPlan,
            capturedRuleFlow);
        ExecutionOptions executionOptions = new()
        {
            AgentRunTimeoutSeconds = 42,
            MaxConsecutiveAgentRunFailures = 7,
        };
        OperationalContextCompactionOptions scanOptions = CreateCompactionOptions(10_000);
        OperationalContextCompactionOptions projectPlanOptions = CreateCompactionOptions(11_000);
        OperationalContextCompactionOptions ruleReviewOptions = CreateCompactionOptions(12_000);
        OperationalContextCompactionOptions reportOptions = CreateCompactionOptions(13_000);
        ProjectReviewAgentCompactionSettings compactionSettings = new()
        {
            Scan = scanOptions,
            ProjectPlan = projectPlanOptions,
            RuleReview = ruleReviewOptions,
            Report = reportOptions,
        };
        InMemoryRuleReviewIssueStore ruleReviewIssueStore = new();
        InMemoryRuleReportIssueStore ruleReportIssueStore = new();
        FakeAgentEventBus agentEventBus = new();

        ProjectReviewWorkflowRunners runners = factory.CreateRunners(
            NoOpChatClient.Instance,
            executionOptions,
            compactionSettings,
            ruleReviewIssueStore,
            ruleReportIssueStore,
            agentEventBus);

        Assert.IsNotNull(runners.ScanWorkflowRunner);
        Assert.IsNotNull(runners.ProjectPlanWorkflowRunner);
        Assert.IsNotNull(runners.RuleFlowWorkflowRunner);
        Assert.AreSame(NoOpChatClient.Instance, capturedScan.Context!.ChatClient);
        Assert.AreSame(capturedScan.Context, capturedProjectPlan.Context);
        Assert.AreSame(capturedScan.Context, capturedRuleFlow.Context);
        Assert.AreSame(executionOptions, capturedScan.Context.ExecutionOptions);
        Assert.AreSame(agentEventBus, capturedScan.Context.AgentEventBus);
        Assert.AreSame(scanOptions, capturedScan.CompactionOptions);
        Assert.AreSame(projectPlanOptions, capturedProjectPlan.CompactionOptions);
        Assert.AreSame(ruleReviewOptions, capturedRuleFlow.RuleReviewCompactionOptions);
        Assert.AreSame(reportOptions, capturedRuleFlow.ReportCompactionOptions);
        Assert.AreSame(ruleReviewIssueStore, capturedRuleFlow.RuleReviewIssueStore);
        Assert.AreSame(ruleReportIssueStore, capturedRuleFlow.RuleReportIssueStore);
    }

    [TestMethod]
    public void WorkflowRunnerFactories_CanResolveFromDependencyInjection()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddScoped<IScanRunnerFactory, ScanRunnerFactory>();
        services.AddScoped<IProjectPlanRunnerFactory, ProjectPlanRunnerFactory>();
        services.AddScoped<IRuleReviewRunnerFactory, RuleReviewRunnerFactory>();
        services.AddScoped<IRuleReportRunnerFactory, RuleReportRunnerFactory>();
        services.AddScoped<IRuleFlowRunnerFactory, RuleFlowRunnerFactory>();
        services.AddScoped<IProjectReviewWorkflowRunnerFactory, ProjectReviewWorkflowRunnerFactory>();
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsInstanceOfType<ScanRunnerFactory>(provider.GetRequiredService<IScanRunnerFactory>());
        Assert.IsInstanceOfType<ProjectPlanRunnerFactory>(provider.GetRequiredService<IProjectPlanRunnerFactory>());
        Assert.IsInstanceOfType<RuleReviewRunnerFactory>(provider.GetRequiredService<IRuleReviewRunnerFactory>());
        Assert.IsInstanceOfType<RuleReportRunnerFactory>(provider.GetRequiredService<IRuleReportRunnerFactory>());
        Assert.IsInstanceOfType<RuleFlowRunnerFactory>(provider.GetRequiredService<IRuleFlowRunnerFactory>());
        Assert.IsInstanceOfType<ProjectReviewWorkflowRunnerFactory>(
            provider.GetRequiredService<IProjectReviewWorkflowRunnerFactory>());
    }

    [TestMethod]
    public void RunnerFactories_UseExpectedSummaryPromptAssets()
    {
        Assert.AreEqual(ScanPromptAssetPaths.ScanSummaryPrompt, ScanRunnerFactory.SummaryPromptAssetPath);
        Assert.AreEqual(ProjectPlanPromptAssetPaths.ProjectPlanSummaryPrompt, ProjectPlanRunnerFactory.SummaryPromptAssetPath);
        Assert.AreEqual(RuleReviewPromptAssetPaths.RuleReviewSummaryPrompt, RuleReviewRunnerFactory.SummaryPromptAssetPath);
        Assert.AreEqual(ReportPromptAssetPaths.ReportSummaryPrompt, RuleReportRunnerFactory.SummaryPromptAssetPath);
    }

    [TestMethod]
    public void RunnerFactories_MapExecutionOptionsToWorkflowOptions()
    {
        ExecutionOptions executionOptions = new()
        {
            AgentRunTimeoutSeconds = 37,
            MaxConsecutiveAgentRunFailures = 9,
        };

        AssertWorkflowOptions(ScanRunnerFactory.CreateWorkflowOptions(executionOptions));
        AssertWorkflowOptions(ProjectPlanRunnerFactory.CreateWorkflowOptions(executionOptions));
        AssertWorkflowOptions(RuleReviewRunnerFactory.CreateWorkflowOptions(executionOptions));
        AssertWorkflowOptions(RuleReportRunnerFactory.CreateWorkflowOptions(executionOptions));

        void AssertWorkflowOptions(dynamic options)
        {
            Assert.AreEqual(TimeSpan.FromSeconds(37), options.AgentRunTimeout);
            Assert.AreEqual(9, options.MaxConsecutiveRunFailures);
        }
    }

    private static OperationalContextCompactionOptions CreateCompactionOptions(long contextWindowTokens) =>
        new()
        {
            ModelContextWindowTokens = contextWindowTokens,
            Mode = OperationalContextCompactionMode.Standard,
        };

    private sealed class CapturedScanRunnerFactory : IScanRunnerFactory
    {
        public WorkflowRuntimeContext? Context { get; private set; }

        public OperationalContextCompactionOptions? CompactionOptions { get; private set; }

        public Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> CreateRunner(
            WorkflowRuntimeContext context,
            OperationalContextCompactionOptions compactionOptions)
        {
            Context = context;
            CompactionOptions = compactionOptions;
            return (_, _) => Task.FromResult(Result.Fail<ScanWorkflowResult>("Not used."));
        }
    }

    private sealed class CapturedProjectPlanRunnerFactory : IProjectPlanRunnerFactory
    {
        public WorkflowRuntimeContext? Context { get; private set; }

        public OperationalContextCompactionOptions? CompactionOptions { get; private set; }

        public Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> CreateRunner(
            WorkflowRuntimeContext context,
            OperationalContextCompactionOptions compactionOptions)
        {
            Context = context;
            CompactionOptions = compactionOptions;
            return (_, _, _) => Task.FromResult(Result.Fail<ProjectPlanWorkflowResult>("Not used."));
        }
    }

    private sealed class CapturedRuleFlowRunnerFactory : IRuleFlowRunnerFactory
    {
        public WorkflowRuntimeContext? Context { get; private set; }

        public OperationalContextCompactionOptions? RuleReviewCompactionOptions { get; private set; }

        public OperationalContextCompactionOptions? ReportCompactionOptions { get; private set; }

        public IRuleReviewIssueStore? RuleReviewIssueStore { get; private set; }

        public IRuleReportIssueStore? RuleReportIssueStore { get; private set; }

        public Func<string, string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> CreateRunner(
            WorkflowRuntimeContext context,
            OperationalContextCompactionOptions ruleReviewCompactionOptions,
            OperationalContextCompactionOptions reportCompactionOptions,
            IRuleReviewIssueStore ruleReviewIssueStore,
            IRuleReportIssueStore ruleReportIssueStore)
        {
            Context = context;
            RuleReviewCompactionOptions = ruleReviewCompactionOptions;
            ReportCompactionOptions = reportCompactionOptions;
            RuleReviewIssueStore = ruleReviewIssueStore;
            RuleReportIssueStore = ruleReportIssueStore;
            return (_, _, _, _, _) => Task.FromResult(Result.Fail<RuleFlowWorkflowResult>("Not used."));
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
