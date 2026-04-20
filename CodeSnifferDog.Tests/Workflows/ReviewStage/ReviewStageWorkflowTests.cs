using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Concurrency;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Workflows.ReviewStage;
using FluentResults;
using RuleReviewModels = CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Tests.Workflows.ReviewStage;

[TestClass]
public sealed class ReviewStageWorkflowTests
{
    [TestMethod]
    public async Task RunAsync_RunsRuleFlowForEachTaskItemAndRule_AndPreservesTaskItemOrder()
    {
        List<string> executedRuleFlowKeys = [];
        RepositoryPreparationWorkflowResult preparationResult = CreatePreparationResult(
            [
                CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2"),
                CreateProjectPlanResult("scan-2", "ProjectTwo", "task-3"),
            ]);
        ReviewStageWorkflow workflow = CreateWorkflow(
            (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
            {
                executedRuleFlowKeys.Add($"{taskItem.ProjectPlanTaskItemId}:{ruleMarkdown}");
                return Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown, RuleFlowCompletionState.ApprovedNoIssue)));
            },
            new ReviewAgentConcurrencyGate(4));

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            preparationResult,
            ["- Rule A", "- Rule B"]);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        CollectionAssert.AreEquivalent(
            new[]
            {
                "task-1:- Rule A",
                "task-1:- Rule B",
                "task-2:- Rule A",
                "task-2:- Rule B",
                "task-3:- Rule A",
                "task-3:- Rule B",
            },
            executedRuleFlowKeys);
        CollectionAssert.AreEqual(
            new[] { "task-1", "task-2" },
            result.Value.ProjectResults[0].ReviewGroupResults.Select(group => group.TaskItem.ProjectPlanTaskItemId).ToArray());
        CollectionAssert.AreEqual(
            new[] { "task-3" },
            result.Value.ProjectResults[1].ReviewGroupResults.Select(group => group.TaskItem.ProjectPlanTaskItemId).ToArray());
        Assert.IsTrue(result.Value.HasAnyReviewGroups);
        Assert.IsTrue(result.Value.AllReviewGroupsFinished);
    }

    [TestMethod]
    public async Task RunAsync_RunsDifferentRulesInParallel_ButSerializesSameRuleAcrossTaskItems()
    {
        Dictionary<string, int> currentRuleConcurrency = [];
        Dictionary<string, int> maxRuleConcurrency = [];
        int currentConcurrency = 0;
        int maxObservedConcurrency = 0;
        RepositoryPreparationWorkflowResult preparationResult = CreatePreparationResult(
            CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2", "task-3"));
        ReviewStageWorkflow workflow = CreateWorkflow(
            async (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
            {
                lock (currentRuleConcurrency)
                {
                    currentRuleConcurrency.TryAdd(ruleMarkdown, 0);
                    maxRuleConcurrency.TryAdd(ruleMarkdown, 0);
                    currentRuleConcurrency[ruleMarkdown]++;
                    maxRuleConcurrency[ruleMarkdown] = Math.Max(maxRuleConcurrency[ruleMarkdown], currentRuleConcurrency[ruleMarkdown]);
                }

                int newConcurrency = Interlocked.Increment(ref currentConcurrency);
                maxObservedConcurrency = Math.Max(maxObservedConcurrency, newConcurrency);

                try
                {
                    await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                    return Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown, RuleFlowCompletionState.ApprovedWithReport));
                }
                finally
                {
                    Interlocked.Decrement(ref currentConcurrency);

                    lock (currentRuleConcurrency)
                        currentRuleConcurrency[ruleMarkdown]--;
                }
            },
            new ReviewAgentConcurrencyGate(4));

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            preparationResult,
            ["- Rule A", "- Rule B"]);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsGreaterThan(1, maxObservedConcurrency);
        Assert.AreEqual(1, maxRuleConcurrency["- Rule A"]);
        Assert.AreEqual(1, maxRuleConcurrency["- Rule B"]);
    }

    [TestMethod]
    public async Task RunAsync_DoesNotCreateMoreRunningFlowsThanAvailableRuleLanes()
    {
        int startedFlows = 0;
        TaskCompletionSource releaseFlows = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RepositoryPreparationWorkflowResult preparationResult = CreatePreparationResult(
            CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2", "task-3"));
        ReviewStageWorkflow workflow = CreateWorkflow(
            async (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
            {
                Interlocked.Increment(ref startedFlows);
                await releaseFlows.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                return Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown, RuleFlowCompletionState.ApprovedNoIssue));
            },
            new ReviewAgentConcurrencyGate(6));

        Task<Result<ReviewStageWorkflowResult>> runTask = workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            preparationResult,
            ["- Rule A", "- Rule B"]);

        await Task.Delay(80);

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
        RepositoryPreparationWorkflowResult preparationResult = CreatePreparationResult(
            [
                CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2", "task-3"),
            ]);
        using ReviewAgentConcurrencyGate concurrencyGate = new(2);
        ReviewStageWorkflow workflow = CreateWorkflow(
            async (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
            {
                lock (startedFlows)
                    startedFlows.Add($"{ruleMarkdown}:{taskItem.ProjectPlanTaskItemId}");

                if (ruleMarkdown == "- Rule B")
                    await holdRuleB.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

                return Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown, RuleFlowCompletionState.ApprovedNoIssue));
            },
            concurrencyGate);

        Task<Result<ReviewStageWorkflowResult>> runTask = workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            preparationResult,
            ["- Rule A", "- Rule B", "- Rule C"]);

        await Task.Delay(100);

        CollectionAssert.AreEqual(
            new[]
            {
                "- Rule A:task-1",
                "- Rule B:task-1",
                "- Rule C:task-1",
            },
            startedFlows.Take(3).ToArray());

        holdRuleB.SetResult();

        Result<ReviewStageWorkflowResult> result = await runTask;
        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
    }

    [TestMethod]
    public async Task RunAsync_SkipsReviewGroups_WhenPreparationDoesNotAdvance()
    {
        bool ruleFlowCalled = false;
        RepositoryPreparationWorkflowResult preparationResult = CreatePreparationResult(
            CreateProjectPlanResult("scan-1", "ProjectOne", "task-1"),
            shouldEnterRuleReview: false);
        ReviewStageWorkflow workflow = CreateWorkflow(
            (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
            {
                ruleFlowCalled = true;
                return Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown, RuleFlowCompletionState.ApprovedNoIssue)));
            },
            new ReviewAgentConcurrencyGate(4));

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            preparationResult,
            ["- Rule A"]);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsFalse(ruleFlowCalled);
        Assert.IsFalse(result.Value.HasAnyReviewGroups);
        Assert.IsFalse(result.Value.AllReviewGroupsFinished);
    }

    [TestMethod]
    public async Task RunAsync_FailsWhenAnyRuleFlowFails()
    {
        RepositoryPreparationWorkflowResult preparationResult = CreatePreparationResult(
            CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2"));
        ReviewStageWorkflow workflow = CreateWorkflow(
            (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
            {
                if (taskItem.ProjectPlanTaskItemId == "task-2" && ruleMarkdown == "- Rule A")
                    return Task.FromResult(Result.Fail<RuleFlowWorkflowResult>("task-2 / Rule A failed."));

                return Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown, RuleFlowCompletionState.ApprovedNoIssue)));
            },
            new ReviewAgentConcurrencyGate(4));

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            preparationResult,
            ["- Rule A"]);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("task-2 / Rule A failed.", StringComparison.Ordinal)));
    }

    private static ReviewStageWorkflow CreateWorkflow(
        Func<string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> ruleFlowWorkflowRunner,
        IReviewAgentConcurrencyGate concurrencyGate) =>
        new(
            new ReviewStageRuleLaneScheduler(ruleFlowWorkflowRunner, concurrencyGate),
            (taskItem, ruleMarkdowns, flowResults) => Result.Ok(CreateReviewGroupResult(taskItem, ruleMarkdowns, flowResults)));

    private static RepositoryPreparationWorkflowResult CreatePreparationResult(
        ProjectPlanWorkflowResult projectPlanResult,
        bool shouldEnterRuleReview = true) =>
        CreatePreparationResult([projectPlanResult], shouldEnterRuleReview);

    private static RepositoryPreparationWorkflowResult CreatePreparationResult(
        IReadOnlyList<ProjectPlanWorkflowResult> projectPlanResults,
        bool shouldEnterRuleReview = true) =>
        new()
        {
            ScanResult = new ScanWorkflowResult
            {
                Projects = projectPlanResults.Select(result => result.ScanProject).ToArray(),
                Verdict = new ReviewVerdict
                {
                    Approved = true,
                    Message = "Scan complete.",
                },
                ScanVerifierApproved = true,
                ContinuedAfterVerifierRejectionLimit = false,
                ShouldEnterProjectPlanning = true,
                ScanAttempts = 1,
                VerifierAttempts = 1,
                ScanAgentResetCount = 0,
            },
            ProjectPlanResults = projectPlanResults,
            ShouldEnterRuleReview = shouldEnterRuleReview,
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
            TaskItems = taskItemIds.Select(CreateTaskItem).ToArray(),
            Verdict = new ReviewVerdict
            {
                Approved = true,
                Message = "Plan complete.",
            },
            ProjectVerifierApproved = true,
            ContinuedAfterVerifierRejectionLimit = false,
            ShouldEnterRuleReview = true,
            PlanAttempts = 1,
            VerifierAttempts = 1,
            ProjectPlanAgentResetCount = 0,
        };

    private static StoredProjectPlanTaskItem CreateTaskItem(string taskItemId) =>
        new()
        {
            ProjectPlanTaskItemId = taskItemId,
            Files =
            [
                new ProjectPlanFile
                {
                    FilePath = $@"src\{taskItemId}\Program.cs",
                    TotalLines = 120,
                },
            ],
        };

    private static ReviewGroupWorkflowResult CreateReviewGroupResult(
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<string> ruleMarkdowns,
        IReadOnlyList<RuleFlowWorkflowResult> flowResults) =>
        new()
        {
            TaskItem = taskItem,
            RuleMarkdowns = ruleMarkdowns.ToArray(),
            FlowResults = flowResults.ToArray(),
            HasAnyRuleFlows = ruleMarkdowns.Count > 0,
            AllRuleFlowsFinished = true,
            ApprovedCompletionCount = flowResults.Count(result => result.IsApprovedCompletion),
            DegradedCompletionCount = flowResults.Count(result => !result.IsApprovedCompletion),
        };

    private static RuleFlowWorkflowResult CreateRuleFlowResult(
        StoredProjectPlanTaskItem taskItem,
        string ruleMarkdown,
        RuleFlowCompletionState completionState)
    {
        bool approved = completionState is RuleFlowCompletionState.ApprovedNoIssue or RuleFlowCompletionState.ApprovedWithReport;
        bool enteredReportAggregation = completionState is RuleFlowCompletionState.ApprovedWithReport or RuleFlowCompletionState.DegradedWithReport;
        bool hasNoIssue = completionState is RuleFlowCompletionState.ApprovedNoIssue or RuleFlowCompletionState.DegradedNoIssue;
        RuleReviewModels.StoredRuleReviewIssue[] reviewIssues = enteredReportAggregation ? [CreateReviewIssue()] : [];

        return new RuleFlowWorkflowResult
        {
            TaskItem = taskItem,
            RuleMarkdown = ruleMarkdown,
            ReviewResult = new RuleReviewModels.RuleReviewWorkflowResult
            {
                TaskItem = taskItem,
                RuleMarkdown = ruleMarkdown,
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
                ReviewVerifierApproved = approved,
                ContinuedAfterVerifierRejectionLimit = !approved && enteredReportAggregation,
                StoppedAfterMissingSubmissionLimit = completionState == RuleFlowCompletionState.DegradedMissingSubmission,
                ShouldEnterReportAggregation = enteredReportAggregation,
                ReviewAttempts = 1,
                VerifierAttempts = 1,
                RuleReviewAgentResetCount = 0,
            },
            ReportResult = enteredReportAggregation ? new RuleReportWorkflowResult
            {
                TaskItem = taskItem,
                RuleMarkdown = ruleMarkdown,
                CurrentFlowIssues = reviewIssues,
                Diff = new RuleReportDiff
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
                ReportVerifierApproved = approved,
                ContinuedAfterVerifierRejectionLimit = !approved,
                AggregatorAttempts = 1,
                VerifierAttempts = 1,
            } : null,
            EnteredReportAggregation = enteredReportAggregation,
            CompletionState = completionState,
        };
    }

    private static RuleReviewModels.StoredRuleReviewIssue CreateReviewIssue() =>
        new()
        {
            RuleReviewIssueId = "review-issue-1",
            IssueType = "Performance",
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

    private static StoredRuleReportIssue CreateReportIssue() =>
        new()
        {
            RuleReportIssueId = "report-issue-1",
            IssueType = "Performance",
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
}
