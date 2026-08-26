using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Workflows.Adapters.AgentFramework.Runtime;
using FluentResults;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using RuleFlowRunnerFactory = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleFlow.RunnerFactory;
using RuleReportRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReport.IRunnerFactory;
using RuleReviewRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReview.IRunnerFactory;
using ReportIssueStore = CodeSnifferDog.Modules.Tools.Report.IIssueStore;
using ReportInMemoryIssueStore = CodeSnifferDog.Modules.Tools.Report.InMemoryIssueStore;
using ReportWorkflowResult = CodeSnifferDog.Models.Report.WorkflowResult;
using ReviewIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.IIssueStore;
using ReviewInMemoryIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.InMemoryIssueStore;
using ReviewStoredIssue = CodeSnifferDog.Models.RuleReview.StoredIssue;
using RuleReviewWorkflowResult = CodeSnifferDog.Models.RuleReview.WorkflowResult;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class RuleFlowRunnerFactoryTests
{
    [TestMethod]
    public async Task CreateRunner_DelegatesReviewAndReportInputsStoresAndCancellationToken()
    {
        CapturingReviewRunnerFactory reviewRunnerFactory = new();
        CapturingReportRunnerFactory reportRunnerFactory = new();
        RuleFlowRunnerFactory factory = new(reviewRunnerFactory, reportRunnerFactory);
        WorkflowRuntimeContext context = new(
            NoOpChatClient.Instance,
            new ExecutionOptions(),
            CompactionOptionsFactory: null!,
            new PromptAssetReader(),
            AgentEventBus: null!,
            WorkflowRuntime: new WorkflowRuntime());
        CompactionOptions reviewCompactionOptions = CreateCompactionOptions(12_000);
        CompactionOptions reportCompactionOptions = CreateCompactionOptions(13_000);
        ReviewInMemoryIssueStore reviewIssueStore = new();
        ReportInMemoryIssueStore reportIssueStore = new();
        StoredTaskItem taskItem = CreateTaskItem();
        using CancellationTokenSource cancellationTokenSource = new();

        Func<string, string, string, StoredTaskItem, CancellationToken, Task<Result<Models.RuleFlow.WorkflowResult>>> runner =
            factory.CreateRunner(
                context,
                reviewCompactionOptions,
                reportCompactionOptions,
                reviewIssueStore,
                reportIssueStore);

        Result<Models.RuleFlow.WorkflowResult> result = await runner(
            "Z:\\repo",
            "rule-a",
            "# Rule A",
            taskItem,
            cancellationTokenSource.Token);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreSame(context, reviewRunnerFactory.Context);
        Assert.AreEqual("Z:\\repo", reviewRunnerFactory.RepositoryRootPath);
        Assert.AreEqual("rule-a", reviewRunnerFactory.RuleKey);
        Assert.AreEqual("# Rule A", reviewRunnerFactory.RuleMarkdown);
        Assert.AreSame(taskItem, reviewRunnerFactory.TaskItem);
        Assert.AreSame(reviewCompactionOptions, reviewRunnerFactory.CompactionOptions);
        Assert.AreSame(reviewIssueStore, reviewRunnerFactory.IssueStore);
        Assert.AreEqual(cancellationTokenSource.Token, reviewRunnerFactory.CancellationToken);

        Assert.AreSame(context, reportRunnerFactory.Context);
        Assert.AreEqual("Z:\\repo", reportRunnerFactory.RepositoryRootPath);
        Assert.AreEqual("rule-a", reportRunnerFactory.RuleKey);
        Assert.AreEqual("# Rule A", reportRunnerFactory.RuleMarkdown);
        Assert.AreSame(taskItem, reportRunnerFactory.TaskItem);
        Assert.AreSame(reviewRunnerFactory.ReviewIssues, reportRunnerFactory.CurrentFlowIssues);
        Assert.AreSame(reportCompactionOptions, reportRunnerFactory.CompactionOptions);
        Assert.AreSame(reportIssueStore, reportRunnerFactory.ReportIssueStore);
        Assert.AreEqual(cancellationTokenSource.Token, reportRunnerFactory.CancellationToken);
    }

    private static CompactionOptions CreateCompactionOptions(long contextWindowTokens) =>
        new()
        {
            ModelContextWindowTokens = contextWindowTokens,
            Mode = CompactionMode.Standard,
        };

    private static StoredTaskItem CreateTaskItem() =>
        new()
        {
            ProjectPlanTaskItemId = "task-a",
            Files =
            [
                new PlanFile
                {
                    FilePath = "Program.cs",
                    TotalLines = 10,
                },
            ],
        };

    private sealed class CapturingReviewRunnerFactory : RuleReviewRunnerFactoryInterface
    {
        public IReadOnlyList<ReviewStoredIssue> ReviewIssues { get; } =
        [
            new()
            {
                RuleReviewIssueId = "issue-a",
                IssueType = "Bug",
                Severity = "High",
                FileOrFunction = "Program.cs",
                RelevantCodePatternOrExpression = "pattern",
                WhyThisIsAProblem = "problem",
                Confidence = "High",
                FollowUpFiles = "none",
                SuggestedFixDirection = "fix",
                ReviewStrategy = "strategy",
                ScopeCoverage = "scope",
                CrossScopeAnalysis = "cross-scope",
            },
        ];

        public WorkflowRuntimeContext? Context { get; private set; }

        public string? RepositoryRootPath { get; private set; }

        public string? RuleKey { get; private set; }

        public string? RuleMarkdown { get; private set; }

        public StoredTaskItem? TaskItem { get; private set; }

        public CompactionOptions? CompactionOptions { get; private set; }

        public ReviewIssueStore? IssueStore { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<Result<RuleReviewWorkflowResult>> RunAsync(
            WorkflowRuntimeContext context,
            string repositoryRootPath,
            string ruleKey,
            string ruleMarkdown,
            StoredTaskItem taskItem,
            CompactionOptions compactionOptions,
            ReviewIssueStore issueStore,
            CancellationToken cancellationToken)
        {
            Context = context;
            RepositoryRootPath = repositoryRootPath;
            RuleKey = ruleKey;
            RuleMarkdown = ruleMarkdown;
            TaskItem = taskItem;
            CompactionOptions = compactionOptions;
            IssueStore = issueStore;
            CancellationToken = cancellationToken;

            return Task.FromResult(Result.Ok(new RuleReviewWorkflowResult
            {
                TaskItem = taskItem,
                RuleKey = ruleKey,
                Issues = ReviewIssues,
                Verdict = new ReviewVerdict
                {
                    Approved = true,
                    Message = "Approved",
                },
                ContinuedAfterVerifierRejectionLimit = false,
                StoppedAfterMissingSubmissionLimit = false,
                ReviewAttempts = 1,
                VerifierAttempts = 1,
                RuleReviewAgentResetCount = 0,
            }));
        }
    }

    private sealed class CapturingReportRunnerFactory : RuleReportRunnerFactoryInterface
    {
        public WorkflowRuntimeContext? Context { get; private set; }

        public string? RepositoryRootPath { get; private set; }

        public string? RuleKey { get; private set; }

        public string? RuleMarkdown { get; private set; }

        public StoredTaskItem? TaskItem { get; private set; }

        public IReadOnlyList<ReviewStoredIssue>? CurrentFlowIssues { get; private set; }

        public CompactionOptions? CompactionOptions { get; private set; }

        public ReportIssueStore? ReportIssueStore { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<Result<ReportWorkflowResult>> RunAsync(
            WorkflowRuntimeContext context,
            string repositoryRootPath,
            string ruleKey,
            string ruleMarkdown,
            StoredTaskItem taskItem,
            IReadOnlyList<ReviewStoredIssue> currentFlowIssues,
            CompactionOptions compactionOptions,
            ReportIssueStore reportIssueStore,
            CancellationToken cancellationToken)
        {
            Context = context;
            RepositoryRootPath = repositoryRootPath;
            RuleKey = ruleKey;
            RuleMarkdown = ruleMarkdown;
            TaskItem = taskItem;
            CurrentFlowIssues = currentFlowIssues;
            CompactionOptions = compactionOptions;
            ReportIssueStore = reportIssueStore;
            CancellationToken = cancellationToken;

            return Task.FromResult(Result.Ok(new ReportWorkflowResult
            {
                RuleKey = ruleKey,
                TaskItem = taskItem,
                Diff = new Diff
                {
                    CreatedIssues = [],
                    UpdatedIssues = [],
                    DeletedIssues = [],
                },
                RepositoryIssues = [],
                Verdict = new ReviewVerdict
                {
                    Approved = true,
                    Message = "Approved",
                },
                ContinuedAfterVerifierRejectionLimit = false,
                AggregatorAttempts = 1,
                VerifierAttempts = 1,
            }));
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
