using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using FluentResults;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class RuleFlowRunnerFactoryTests
{
    [TestMethod]
    public async Task CreateRunner_DelegatesReviewAndReportInputsStoresAndCancellationToken()
    {
        CapturingRuleReviewRunnerFactory reviewRunnerFactory = new();
        CapturingRuleReportRunnerFactory reportRunnerFactory = new();
        RuleFlowRunnerFactory factory = new(reviewRunnerFactory, reportRunnerFactory);
        WorkflowRuntimeContext context = new(
            NoOpChatClient.Instance,
            new ExecutionOptions(),
            CompactionOptionsFactory: null!,
            new PromptAssetReader(),
            AgentEventBus: null!);
        OperationalContextCompactionOptions reviewCompactionOptions = CreateCompactionOptions(12_000);
        OperationalContextCompactionOptions reportCompactionOptions = CreateCompactionOptions(13_000);
        InMemoryRuleReviewIssueStore reviewIssueStore = new();
        InMemoryRuleReportIssueStore reportIssueStore = new();
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        using CancellationTokenSource cancellationTokenSource = new();

        Func<string, string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<Models.RuleFlow.RuleFlowWorkflowResult>>> runner =
            factory.CreateRunner(
                context,
                reviewCompactionOptions,
                reportCompactionOptions,
                reviewIssueStore,
                reportIssueStore);

        Result<Models.RuleFlow.RuleFlowWorkflowResult> result = await runner(
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

    private static OperationalContextCompactionOptions CreateCompactionOptions(long contextWindowTokens) =>
        new()
        {
            ModelContextWindowTokens = contextWindowTokens,
            Mode = OperationalContextCompactionMode.Standard,
        };

    private static StoredProjectPlanTaskItem CreateTaskItem() =>
        new()
        {
            ProjectPlanTaskItemId = "task-a",
            Files =
            [
                new ProjectPlanFile
                {
                    FilePath = "Program.cs",
                    TotalLines = 10,
                },
            ],
        };

    private sealed class CapturingRuleReviewRunnerFactory : IRuleReviewRunnerFactory
    {
        public IReadOnlyList<StoredRuleReviewIssue> ReviewIssues { get; } =
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

        public StoredProjectPlanTaskItem? TaskItem { get; private set; }

        public OperationalContextCompactionOptions? CompactionOptions { get; private set; }

        public IRuleReviewIssueStore? IssueStore { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<Result<RuleReviewWorkflowResult>> RunAsync(
            WorkflowRuntimeContext context,
            string repositoryRootPath,
            string ruleKey,
            string ruleMarkdown,
            StoredProjectPlanTaskItem taskItem,
            OperationalContextCompactionOptions compactionOptions,
            IRuleReviewIssueStore issueStore,
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

    private sealed class CapturingRuleReportRunnerFactory : IRuleReportRunnerFactory
    {
        public WorkflowRuntimeContext? Context { get; private set; }

        public string? RepositoryRootPath { get; private set; }

        public string? RuleKey { get; private set; }

        public string? RuleMarkdown { get; private set; }

        public StoredProjectPlanTaskItem? TaskItem { get; private set; }

        public IReadOnlyList<StoredRuleReviewIssue>? CurrentFlowIssues { get; private set; }

        public OperationalContextCompactionOptions? CompactionOptions { get; private set; }

        public IRuleReportIssueStore? ReportIssueStore { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<Result<RuleReportWorkflowResult>> RunAsync(
            WorkflowRuntimeContext context,
            string repositoryRootPath,
            string ruleKey,
            string ruleMarkdown,
            StoredProjectPlanTaskItem taskItem,
            IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues,
            OperationalContextCompactionOptions compactionOptions,
            IRuleReportIssueStore reportIssueStore,
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

            return Task.FromResult(Result.Ok(new RuleReportWorkflowResult
            {
                RuleKey = ruleKey,
                TaskItem = taskItem,
                Diff = new RuleReportDiff
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
