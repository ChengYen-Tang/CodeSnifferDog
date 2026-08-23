using CodeSnifferDog.Agents.ProjectPlan;
using CodeSnifferDog.Agents.Report;
using CodeSnifferDog.Agents.RuleReview;
using CodeSnifferDog.Agents.Scan;
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
using CodeSnifferDog.Workflows.Adapters.AgentFramework.Runtime;
using FluentResults;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using ProjectPlanRunnerFactory = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.ProjectPlan.RunnerFactory;
using ProjectPlanRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.ProjectPlan.IRunnerFactory;
using RuleFlowRunnerFactory = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleFlow.RunnerFactory;
using RuleFlowRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleFlow.IRunnerFactory;
using RuleReportRunnerFactory = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReport.RunnerFactory;
using RuleReportRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReport.IRunnerFactory;
using RuleReviewRunnerFactory = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReview.RunnerFactory;
using RuleReviewRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReview.IRunnerFactory;
using ReportIssueStore = CodeSnifferDog.Modules.Tools.Report.IIssueStore;
using ReportInMemoryIssueStore = CodeSnifferDog.Modules.Tools.Report.InMemoryIssueStore;
using ReviewIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.IIssueStore;
using ReviewInMemoryIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.InMemoryIssueStore;
using ProjectPlanWorkflowResult = CodeSnifferDog.Models.ProjectPlan.WorkflowResult;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ReviewRunnerFactoryTests
{
    [TestMethod]
    public void CreateRunners_DelegatesSharedContextOptionsStoresAndEventBusToRunnerBuilders()
    {
        CapturedScanRunnerFactory capturedScan = new();
        CapturedPlanRunnerFactory capturedProjectPlan = new();
        CapturedRuleFlowRunnerFactory capturedRuleFlow = new();
        ReviewRunnerFactory factory = new(
            capturedScan,
            capturedProjectPlan,
            capturedRuleFlow);
        ExecutionOptions executionOptions = new()
        {
            AgentRunTimeoutSeconds = 42,
            MaxConsecutiveAgentRunFailures = 7,
        };
        CompactionOptions scanOptions = CreateCompactionOptions(10_000);
        CompactionOptions projectPlanOptions = CreateCompactionOptions(11_000);
        CompactionOptions ruleReviewOptions = CreateCompactionOptions(12_000);
        CompactionOptions reportOptions = CreateCompactionOptions(13_000);
        Settings compactionSettings = new()
        {
            Scan = scanOptions,
            ProjectPlan = projectPlanOptions,
            RuleReview = ruleReviewOptions,
            Report = reportOptions,
        };
        ReviewInMemoryIssueStore ruleReviewIssueStore = new();
        ReportInMemoryIssueStore ruleReportIssueStore = new();
        FakeAgentEventBus agentEventBus = new();

        ReviewRunners runners = factory.CreateRunners(
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
        Assert.IsInstanceOfType<WorkflowRuntime>(capturedScan.Context.WorkflowRuntime);
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
        services.AddScoped<ProjectPlanRunnerFactoryInterface, ProjectPlanRunnerFactory>();
        services.AddScoped<RuleReviewRunnerFactoryInterface, RuleReviewRunnerFactory>();
        services.AddScoped<RuleReportRunnerFactoryInterface, RuleReportRunnerFactory>();
        services.AddScoped<RuleFlowRunnerFactoryInterface, RuleFlowRunnerFactory>();
        services.AddScoped<IReviewRunnerFactory, ReviewRunnerFactory>();
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsInstanceOfType<ScanRunnerFactory>(provider.GetRequiredService<IScanRunnerFactory>());
        Assert.IsInstanceOfType<ProjectPlanRunnerFactory>(provider.GetRequiredService<ProjectPlanRunnerFactoryInterface>());
        Assert.IsInstanceOfType<RuleReviewRunnerFactory>(provider.GetRequiredService<RuleReviewRunnerFactoryInterface>());
        Assert.IsInstanceOfType<RuleReportRunnerFactory>(provider.GetRequiredService<RuleReportRunnerFactoryInterface>());
        Assert.IsInstanceOfType<RuleFlowRunnerFactory>(provider.GetRequiredService<RuleFlowRunnerFactoryInterface>());
        Assert.IsInstanceOfType<ReviewRunnerFactory>(
            provider.GetRequiredService<IReviewRunnerFactory>());
    }

    [TestMethod]
    public void RunnerFactories_UseExpectedSummaryPromptAssets()
    {
        Assert.AreEqual(ScanAgentPromptAssets.ScanSummaryPrompt, ScanRunnerFactory.SummaryPromptAssetPath);
        Assert.AreEqual(ProjectPlanAgentPromptAssets.ProjectPlanSummaryPrompt, ProjectPlanRunnerFactory.SummaryPromptAssetPath);
        Assert.AreEqual(RuleReviewAgentPromptAssets.RuleReviewSummaryPrompt, RuleReviewRunnerFactory.SummaryPromptAssetPath);
        Assert.AreEqual(ReportAgentPromptAssets.ReportSummaryPrompt, RuleReportRunnerFactory.SummaryPromptAssetPath);
    }

    [TestMethod]
    public void RunnerFactories_MapExecutionOptionsToWorkflowOptions()
    {
        ExecutionOptions executionOptions = new()
        {
            AgentRunTimeoutSeconds = 37,
            MaxConsecutiveAgentRunFailures = 9,
            MaxMissingSubmissionAttempts = 11,
            MaxVerifierRejectionAttempts = 12,
        };

        ScanWorkflowOptions scanOptions = ScanRunnerFactory.CreateWorkflowOptions(executionOptions);
        AssertWorkflowOptions(scanOptions);
        Assert.AreEqual(12, scanOptions.MaxScanAgentResets);

        CodeSnifferDog.Models.ProjectPlan.WorkflowOptions projectPlanOptions = ProjectPlanRunnerFactory.CreateWorkflowOptions(executionOptions);
        AssertWorkflowOptions(projectPlanOptions);
        Assert.AreEqual(12, projectPlanOptions.MaxProjectPlanAgentResets);

        CodeSnifferDog.Models.RuleReview.WorkflowOptions ruleReviewOptions = RuleReviewRunnerFactory.CreateWorkflowOptions(executionOptions);
        AssertWorkflowOptions(ruleReviewOptions);
        Assert.AreEqual(12, ruleReviewOptions.MaxRuleReviewAgentResets);

        CodeSnifferDog.Models.Report.WorkflowOptions reportOptions = RuleReportRunnerFactory.CreateWorkflowOptions(executionOptions);
        AssertWorkflowOptions(reportOptions);

        void AssertWorkflowOptions(dynamic options)
        {
            Assert.AreEqual(TimeSpan.FromSeconds(37), options.AgentRunTimeout);
            Assert.AreEqual(9, options.MaxConsecutiveRunFailures);
            Assert.AreEqual(11, options.MaxMissingSubmissionAttempts);
            Assert.AreEqual(12, options.MaxVerifierRejectionAttempts);
        }
    }

    private static CompactionOptions CreateCompactionOptions(long contextWindowTokens) =>
        new()
        {
            ModelContextWindowTokens = contextWindowTokens,
            Mode = CompactionMode.Standard,
        };

    private sealed class CapturedScanRunnerFactory : IScanRunnerFactory
    {
        public WorkflowRuntimeContext? Context { get; private set; }

        public CompactionOptions? CompactionOptions { get; private set; }

        public Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> CreateRunner(
            WorkflowRuntimeContext context,
            CompactionOptions compactionOptions)
        {
            Context = context;
            CompactionOptions = compactionOptions;
            return (_, _) => Task.FromResult(Result.Fail<ScanWorkflowResult>("Not used."));
        }
    }

    private sealed class CapturedPlanRunnerFactory : ProjectPlanRunnerFactoryInterface
    {
        public WorkflowRuntimeContext? Context { get; private set; }

        public CompactionOptions? CompactionOptions { get; private set; }

        public Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> CreateRunner(
            WorkflowRuntimeContext context,
            CompactionOptions compactionOptions)
        {
            Context = context;
            CompactionOptions = compactionOptions;
            return (_, _, _) => Task.FromResult(Result.Fail<ProjectPlanWorkflowResult>("Not used."));
        }
    }

    private sealed class CapturedRuleFlowRunnerFactory : RuleFlowRunnerFactoryInterface
    {
        public WorkflowRuntimeContext? Context { get; private set; }

        public CompactionOptions? RuleReviewCompactionOptions { get; private set; }

        public CompactionOptions? ReportCompactionOptions { get; private set; }

        public ReviewIssueStore? RuleReviewIssueStore { get; private set; }

        public ReportIssueStore? RuleReportIssueStore { get; private set; }

        public Func<string, string, string, StoredTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> CreateRunner(
            WorkflowRuntimeContext context,
            CompactionOptions ruleReviewCompactionOptions,
            CompactionOptions reportCompactionOptions,
            ReviewIssueStore ruleReviewIssueStore,
            ReportIssueStore ruleReportIssueStore)
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
