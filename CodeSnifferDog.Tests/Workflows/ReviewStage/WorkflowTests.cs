using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Concurrency;
using CodeSnifferDog.Workflows.ReviewStage;
using FluentResults;
using ProjectPlanWorkflowResult = CodeSnifferDog.Models.ProjectPlan.WorkflowResult;
using PreparationWorkflowResult = CodeSnifferDog.Models.Preparation.WorkflowResult;
using ReportWorkflowResult = CodeSnifferDog.Models.Report.WorkflowResult;
using ReviewGroupWorkflowResult = CodeSnifferDog.Models.ReviewGroup.WorkflowResult;
using ReviewStageWorkflowResult = CodeSnifferDog.Models.ReviewStage.WorkflowResult;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;
using RuleReviewModels = CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.ReviewAgentTeam.Scheduling;

namespace CodeSnifferDog.Tests.Workflows.ReviewStage;

[TestClass]
public sealed class WorkflowTests
{
    public required TestContext TestContext { get; init; }

    private static readonly string[] ExecutedRuleFlowKeys =
    [
        "task-1:rule-a",
        "task-1:rule-b",
        "task-2:rule-a",
        "task-2:rule-b",
        "task-3:rule-a",
        "task-3:rule-b",
    ];

    private static readonly string[] FirstProjectTaskIds =
    [
        "task-1",
        "task-2",
    ];

    private static readonly string[] SecondProjectTaskIds =
    [
        "task-3",
    ];

    private static readonly string[] PrioritizedLaneOrder =
    [
        "rule-a:task-1",
        "rule-b:task-1",
        "rule-c:task-1",
    ];

    [TestMethod]
    public async Task RunAsync_RunsRuleFlowForEachTaskItemAndRule_AndPreservesTaskItemOrder()
    {
        List<string> executedRuleFlowKeys = [];
        PreparationWorkflowResult preparationResult = CreatePreparationResult(
            [
                CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2"),
                CreateProjectPlanResult("scan-2", "ProjectTwo", "task-3"),
            ]);
        Workflow workflow = CreateWorkflow(
            (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
            {
                executedRuleFlowKeys.Add($"{taskItem.ProjectPlanTaskItemId}:{ruleKey}");
                return Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleKey, ruleMarkdown, CompletionState.ApprovedNoIssue)));
            },
            new ReviewAgentConcurrencyGate(4));

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            preparationResult,
            CreateRuleDefinitions(("rule-a", "- Rule A"), ("rule-b", "- Rule B")),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        CollectionAssert.AreEquivalent(
            ExecutedRuleFlowKeys,
            executedRuleFlowKeys);
        CollectionAssert.AreEqual(
            FirstProjectTaskIds,
            result.Value.ProjectResults[0].ReviewGroupResults.Select(group => group.TaskItem.ProjectPlanTaskItemId).ToArray());
        CollectionAssert.AreEqual(
            SecondProjectTaskIds,
            result.Value.ProjectResults[1].ReviewGroupResults.Select(group => group.TaskItem.ProjectPlanTaskItemId).ToArray());
        Assert.IsTrue(result.Value.ProjectResults.Any(project => project.ReviewGroupResults.Count > 0));
    }

    [TestMethod]
    public async Task RunAsync_RunsDifferentRulesInParallel_ButSerializesSameRuleAcrossTaskItems()
    {
        Dictionary<string, int> currentRuleConcurrency = [];
        Dictionary<string, int> maxRuleConcurrency = [];
        int currentConcurrency = 0;
        int maxObservedConcurrency = 0;
        PreparationWorkflowResult preparationResult = CreatePreparationResult(
            CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2", "task-3"));
        Workflow workflow = CreateWorkflow(
            async (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
            {
                lock (currentRuleConcurrency)
                {
                    currentRuleConcurrency.TryAdd(ruleKey, 0);
                    maxRuleConcurrency.TryAdd(ruleKey, 0);
                    currentRuleConcurrency[ruleKey]++;
                    maxRuleConcurrency[ruleKey] = Math.Max(maxRuleConcurrency[ruleKey], currentRuleConcurrency[ruleKey]);
                }

                int newConcurrency = Interlocked.Increment(ref currentConcurrency);
                maxObservedConcurrency = Math.Max(maxObservedConcurrency, newConcurrency);

                try
                {
                    await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                    return Result.Ok(CreateRuleFlowResult(taskItem, ruleKey, ruleMarkdown, CompletionState.ApprovedWithReport));
                }
                finally
                {
                    Interlocked.Decrement(ref currentConcurrency);

                    lock (currentRuleConcurrency)
                        currentRuleConcurrency[ruleKey]--;
                }
            },
            new ReviewAgentConcurrencyGate(4));

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            preparationResult,
            CreateRuleDefinitions(("rule-a", "- Rule A"), ("rule-b", "- Rule B")),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsGreaterThan(1, maxObservedConcurrency);
        Assert.AreEqual(1, maxRuleConcurrency["rule-a"]);
        Assert.AreEqual(1, maxRuleConcurrency["rule-b"]);
    }

    [TestMethod]
    public async Task RunAsync_DoesNotCreateMoreRunningFlowsThanAvailableRuleLanes()
    {
        int startedFlows = 0;
        TaskCompletionSource releaseFlows = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PreparationWorkflowResult preparationResult = CreatePreparationResult(
            CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2", "task-3"));
        Workflow workflow = CreateWorkflow(
            async (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
            {
                Interlocked.Increment(ref startedFlows);
                await releaseFlows.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return Result.Ok(CreateRuleFlowResult(taskItem, ruleKey, ruleMarkdown, CompletionState.ApprovedNoIssue));
            },
            new ReviewAgentConcurrencyGate(6));

        Task<Result<ReviewStageWorkflowResult>> runTask = workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            preparationResult,
            CreateRuleDefinitions(("rule-a", "- Rule A"), ("rule-b", "- Rule B")),
            TestContext.CancellationToken);

        await Task.Delay(80, TestContext.CancellationToken);

        Assert.AreEqual(2, Volatile.Read(ref startedFlows));

        releaseFlows.SetResult();

        Result<ReviewStageWorkflowResult> result = await runTask;
        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
    }

    [TestMethod]
    public async Task RunAsync_PrioritizesEligibleLaneWithLargestRemainingQueue()
    {
        List<string> startedFlows = [];
        TaskCompletionSource holdRuleB = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PreparationWorkflowResult preparationResult = CreatePreparationResult(
            [
                CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2", "task-3"),
            ]);
        using ReviewAgentConcurrencyGate concurrencyGate = new(2);
        Workflow workflow = CreateWorkflow(
            async (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
            {
                lock (startedFlows)
                    startedFlows.Add($"{ruleKey}:{taskItem.ProjectPlanTaskItemId}");

                if (ruleKey == "rule-b")
                    await holdRuleB.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

                return Result.Ok(CreateRuleFlowResult(taskItem, ruleKey, ruleMarkdown, CompletionState.ApprovedNoIssue));
            },
            concurrencyGate);

        Task<Result<ReviewStageWorkflowResult>> runTask = workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            preparationResult,
            CreateRuleDefinitions(("rule-a", "- Rule A"), ("rule-b", "- Rule B"), ("rule-c", "- Rule C")),
            TestContext.CancellationToken);

        await Task.Delay(100, TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            PrioritizedLaneOrder,
            startedFlows.Take(3).ToArray());

        holdRuleB.SetResult();

        Result<ReviewStageWorkflowResult> result = await runTask;
        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
    }

    [TestMethod]
    public async Task RunAsync_SkipsReviewGroups_WhenPreparationDoesNotAdvance()
    {
        bool ruleFlowCalled = false;
        PreparationWorkflowResult preparationResult = CreatePreparationResult(
            CreateProjectPlanResult("scan-1", "ProjectOne", "task-1"),
            shouldEnterRuleReview: false);
        Workflow workflow = CreateWorkflow(
            (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
            {
                ruleFlowCalled = true;
                return Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleKey, ruleMarkdown, CompletionState.ApprovedNoIssue)));
            },
            new ReviewAgentConcurrencyGate(4));

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            preparationResult,
            CreateRuleDefinitions(("rule-a", "- Rule A")),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsFalse(ruleFlowCalled);
        Assert.IsFalse(result.Value.ProjectResults.Any(project => project.ReviewGroupResults.Count > 0));
    }

    [TestMethod]
    public async Task RunAsync_ReturnsEmptyReviewGroups_WhenProjectPlansContainNoTaskItems()
    {
        bool ruleFlowCalled = false;
        PreparationWorkflowResult preparationResult = CreatePreparationResult(
            CreateProjectPlanResult("scan-1", "ProjectOne"));
        Workflow workflow = CreateWorkflow(
            (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
            {
                ruleFlowCalled = true;
                return Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleKey, ruleMarkdown, CompletionState.ApprovedNoIssue)));
            },
            new ReviewAgentConcurrencyGate(4));

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            preparationResult,
            CreateRuleDefinitions(("rule-a", "- Rule A")),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsFalse(ruleFlowCalled);
        Assert.HasCount(1, result.Value.ProjectResults);
        Assert.IsEmpty(result.Value.ProjectResults[0].ReviewGroupResults);
    }

    [TestMethod]
    public async Task RunAsync_PublishesSequentialReviewGroupDisplayNames()
    {
        RecordingAgentEventBus eventBus = new();
        PreparationWorkflowResult preparationResult = CreatePreparationResult(
            [
                CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2"),
                CreateProjectPlanResult("scan-2", "ProjectTwo", "task-3"),
            ]);
        Workflow workflow = CreateWorkflow(
            (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleKey, ruleMarkdown, CompletionState.ApprovedNoIssue))),
            new ReviewAgentConcurrencyGate(4),
            eventBus);

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            preparationResult,
            CreateRuleDefinitions(("rule-a", "- Rule A")),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        CollectionAssert.AreEqual(
            new[] { "Review: 1", "Review: 2", "Review: 3" },
            eventBus.GroupCreatedDisplayNames.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_FailsWhenAnyRuleFlowFails()
    {
        PreparationWorkflowResult preparationResult = CreatePreparationResult(
            CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2"));
        Workflow workflow = CreateWorkflow(
            (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
            {
                if (taskItem.ProjectPlanTaskItemId == "task-2" && ruleKey == "rule-a")
                    return Task.FromResult(Result.Fail<RuleFlowWorkflowResult>("task-2 / Rule A failed."));

                return Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleKey, ruleMarkdown, CompletionState.ApprovedNoIssue)));
            },
            new ReviewAgentConcurrencyGate(4));

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            preparationResult,
            CreateRuleDefinitions(("rule-a", "- Rule A")),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("task-2 / Rule A failed.", StringComparison.Ordinal)));
    }

    private static Workflow CreateWorkflow(
        Func<string, string, string, StoredTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> ruleFlowWorkflowRunner,
        ReviewAgentConcurrencyGate concurrencyGate,
        IAgentEventBus? agentEventBus = null) =>
        new(
            new RuleLaneScheduler(ruleFlowWorkflowRunner, concurrencyGate),
            (taskItem, ruleDefinitions, flowResults) => Result.Ok(CreateReviewGroupResult(taskItem, ruleDefinitions, flowResults)),
            agentEventBus);

    private static RuleDefinition[] CreateRuleDefinitions(params (string RuleKey, string RuleMarkdown)[] definitions) =>
        [.. definitions.Select(definition => new RuleDefinition
        {
            RuleKey = definition.RuleKey,
            RuleMarkdown = definition.RuleMarkdown,
        })];

    private static PreparationWorkflowResult CreatePreparationResult(
        ProjectPlanWorkflowResult projectPlanResult,
        bool shouldEnterRuleReview = true) =>
        CreatePreparationResult([projectPlanResult], shouldEnterRuleReview);

    private static PreparationWorkflowResult CreatePreparationResult(
        ProjectPlanWorkflowResult[] projectPlanResults,
        bool shouldEnterRuleReview = true) =>
        new()
        {
            ScanResult = new ScanWorkflowResult
            {
                Projects = [.. projectPlanResults.Select(result => result.ScanProject)],
                Verdict = new ReviewVerdict
                {
                    Approved = true,
                    Message = "Scan complete.",
                },
                ScanAttempts = 1,
                VerifierAttempts = 1,
                ScanAgentResetCount = 0,
            },
            ProjectPlanResults = shouldEnterRuleReview ? projectPlanResults : [],
        };

    private static ProjectPlanWorkflowResult CreateProjectPlanResult(
        string scanProjectId,
        string projectName,
        params string[] taskItemIds) =>
        new()
        {
            ScanProject = new StoredScanProject
            {
                ScanProjectId = scanProjectId,
                ProjectName = projectName,
                ProjectPath = $@"src\{projectName}\{projectName}.csproj",
                ProjectType = ".csproj",
                Reason = $"Reason for {projectName}.",
            },
            TaskItems = [.. taskItemIds.Select(CreateTaskItem)],
            Verdict = new ReviewVerdict
            {
                Approved = true,
                Message = "Plan complete.",
            },
            ContinuedAfterVerifierRejectionLimit = false,
            PlanAttempts = 1,
            VerifierAttempts = 1,
            ProjectPlanAgentResetCount = 0,
        };

    private static StoredTaskItem CreateTaskItem(string taskItemId) =>
        new()
        {
            ProjectPlanTaskItemId = taskItemId,
            Files =
            [
                new PlanFile
                {
                    FilePath = $@"src\{taskItemId}\Program.cs",
                    TotalLines = 120,
                },
            ],
        };

    private static ReviewGroupWorkflowResult CreateReviewGroupResult(
        StoredTaskItem taskItem,
        IReadOnlyList<RuleDefinition> ruleDefinitions,
        IReadOnlyList<RuleFlowWorkflowResult> flowResults) =>
        new()
        {
            TaskItem = taskItem,
            FlowResults = [.. flowResults],
        };

    private static RuleFlowWorkflowResult CreateRuleFlowResult(
        StoredTaskItem taskItem,
        string ruleKey,
        string _,
        CompletionState completionState)
    {
        bool approved = completionState is CompletionState.ApprovedNoIssue or CompletionState.ApprovedWithReport;
        bool enteredReportAggregation = completionState is CompletionState.ApprovedWithReport or CompletionState.DegradedWithReport;
        bool hasNoIssue = completionState is CompletionState.ApprovedNoIssue or CompletionState.DegradedNoIssue;
        RuleReviewModels.StoredIssue[] reviewIssues = enteredReportAggregation ? [CreateReviewIssue()] : [];

        return new RuleFlowWorkflowResult
        {
            ReviewResult = new RuleReviewModels.WorkflowResult
            {
                TaskItem = taskItem,
                RuleKey = ruleKey,
                Issues = reviewIssues,
                NoIssueConclusion = hasNoIssue ? new RuleReviewModels.NoIssueConclusion
                {
                    ReviewStrategy = "Reviewed the entry point.",
                    ScopeCoverage = "Inspected Program.cs.",
                    CrossScopeAnalysis = "No further tracing was required.",
                    WhyNoIssueWasFound = "No issue matched the rule.",
                } : null,
                Verdict = new ReviewVerdict
                {
                    Approved = approved,
                    Message = approved ? "Review accepted." : "Review degraded.",
                },
                ContinuedAfterVerifierRejectionLimit = !approved && enteredReportAggregation,
                StoppedAfterMissingSubmissionLimit = completionState == CompletionState.DegradedMissingSubmission,
                ReviewAttempts = 1,
                VerifierAttempts = 1,
                RuleReviewAgentResetCount = 0,
            },
            ReportResult = enteredReportAggregation ? new ReportWorkflowResult
            {
                RuleKey = ruleKey,
                TaskItem = taskItem,
                Diff = new Diff
                {
                    CreatedIssues = [CreateReportIssue()],
                    UpdatedIssues = [],
                    DeletedIssues = [],
                },
                RepositoryIssues = [CreateReportIssue()],
                Verdict = new ReviewVerdict
                {
                    Approved = approved,
                    Message = approved ? "Report accepted." : "Report degraded.",
                },
                ContinuedAfterVerifierRejectionLimit = !approved,
                AggregatorAttempts = 1,
                VerifierAttempts = 1,
            } : null,
            CompletionState = completionState,
        };
    }

    private static RuleReviewModels.StoredIssue CreateReviewIssue() =>
        new()
        {
            RuleReviewIssueId = "review-issue-1",
            IssueType = "Performance",
            Severity = "High",
            FileOrFunction = "Program.cs",
            RelevantCodePatternOrExpression = "Repeated synchronous call",
            WhyThisIsAProblem = "This blocks the request path.",
            Confidence = "High",
            FollowUpFiles = "Service.cs",
            SuggestedFixDirection = "Use the cached async path.",
            ReviewStrategy = "Reviewed the hot path first.",
            ScopeCoverage = "Inspected Program.cs.",
            CrossScopeAnalysis = "Followed the call into Service.cs.",
        };

    private static StoredIssue CreateReportIssue() =>
        new()
        {
            RuleReportIssueId = "report-issue-1",
            IssueType = "Performance",
            Severity = "High",
            FileOrFunction = "Program.cs",
            RelevantCodePatternOrExpression = "Repeated synchronous call",
            WhyThisIsAProblem = "This blocks the request path.",
            Confidence = "High",
            FollowUpFiles = "Service.cs",
            SuggestedFixDirection = "Use the cached async path.",
            ReviewStrategy = "Reviewed the hot path first.",
            ScopeCoverage = "Inspected Program.cs.",
            CrossScopeAnalysis = "Followed the call into Service.cs.",
        };

    private sealed class RecordingAgentEventBus : IAgentEventBus
    {
        public List<string> GroupCreatedDisplayNames { get; } = [];

        public IAgentEventScope CreateScope(string groupKey, string agentKey) =>
            throw new NotSupportedException();

        public ValueTask PublishGroupCreatedAsync(
            string groupKey,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            GroupCreatedDisplayNames.Add(displayName);
            return ValueTask.CompletedTask;
        }
    }
}
