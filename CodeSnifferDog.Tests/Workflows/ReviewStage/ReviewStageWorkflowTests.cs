using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using RuleReviewModels = CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Workflows.ReviewStage;
using FluentResults;

namespace CodeSnifferDog.Tests.Workflows.ReviewStage;

[TestClass]
public sealed class ReviewStageWorkflowTests
{
    [TestMethod]
    public async Task RunAsync_RunsReviewGroupForEachTaskItem_AndPreservesOrder()
    {
        List<string> executedTaskItemIds = [];
        RepositoryPreparationWorkflowResult preparationResult = CreatePreparationResult(
            [
                CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2"),
                CreateProjectPlanResult("scan-2", "ProjectTwo", "task-3"),
            ]);
        ReviewStageWorkflow workflow = new(
            (repositoryRootPath, taskItem, ruleMarkdowns, cancellationToken) =>
            {
                executedTaskItemIds.Add(taskItem.ProjectPlanTaskItemId);
                return Task.FromResult(Result.Ok(CreateReviewGroupResult(taskItem, ruleMarkdowns)));
            });

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            preparationResult,
            ["- Rule A", "- Rule B"]);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        CollectionAssert.AreEqual(new[] { "task-1", "task-2", "task-3" }, executedTaskItemIds);
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
    public async Task RunAsync_RespectsParallelLimit()
    {
        int currentConcurrency = 0;
        int maxObservedConcurrency = 0;
        RepositoryPreparationWorkflowResult preparationResult = CreatePreparationResult(
            CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2", "task-3"));
        ReviewStageWorkflow workflow = new(
            async (repositoryRootPath, taskItem, ruleMarkdowns, cancellationToken) =>
            {
                int newConcurrency = Interlocked.Increment(ref currentConcurrency);
                maxObservedConcurrency = Math.Max(maxObservedConcurrency, newConcurrency);

                try
                {
                    await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                    return Result.Ok(CreateReviewGroupResult(taskItem, ruleMarkdowns));
                }
                finally
                {
                    Interlocked.Decrement(ref currentConcurrency);
                }
            },
            new ReviewStageWorkflowOptions
            {
                MaxConcurrentReviewGroups = 2,
            });

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            preparationResult,
            ["- Rule A"]);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, maxObservedConcurrency);
    }

    [TestMethod]
    public async Task RunAsync_SkipsReviewGroups_WhenPreparationDoesNotAdvance()
    {
        bool reviewGroupCalled = false;
        RepositoryPreparationWorkflowResult preparationResult = CreatePreparationResult(
            CreateProjectPlanResult("scan-1", "ProjectOne", "task-1"),
            shouldEnterRuleReview: false);
        ReviewStageWorkflow workflow = new(
            (repositoryRootPath, taskItem, ruleMarkdowns, cancellationToken) =>
            {
                reviewGroupCalled = true;
                return Task.FromResult(Result.Ok(CreateReviewGroupResult(taskItem, ruleMarkdowns)));
            });

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            preparationResult,
            ["- Rule A"]);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsFalse(reviewGroupCalled);
        Assert.IsFalse(result.Value.HasAnyReviewGroups);
        Assert.IsFalse(result.Value.AllReviewGroupsFinished);
    }

    [TestMethod]
    public async Task RunAsync_FailsWhenAnyReviewGroupFails()
    {
        RepositoryPreparationWorkflowResult preparationResult = CreatePreparationResult(
            CreateProjectPlanResult("scan-1", "ProjectOne", "task-1", "task-2"));
        ReviewStageWorkflow workflow = new(
            (repositoryRootPath, taskItem, ruleMarkdowns, cancellationToken) =>
            {
                if (taskItem.ProjectPlanTaskItemId == "task-2")
                    return Task.FromResult(Result.Fail<ReviewGroupWorkflowResult>("task-2 failed."));

                return Task.FromResult(Result.Ok(CreateReviewGroupResult(taskItem, ruleMarkdowns)));
            });

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            preparationResult,
            ["- Rule A"]);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("task-2 failed.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_FailsWhenParallelLimitIsInvalid()
    {
        ReviewStageWorkflow workflow = new(
            (repositoryRootPath, taskItem, ruleMarkdowns, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateReviewGroupResult(taskItem, ruleMarkdowns))),
            new ReviewStageWorkflowOptions
            {
                MaxConcurrentReviewGroups = 0,
            });

        Result<ReviewStageWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            CreatePreparationResult(CreateProjectPlanResult("scan-1", "ProjectOne", "task-1")),
            ["- Rule A"]);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error => error.Message.Contains("MaxConcurrentReviewGroups", StringComparison.Ordinal)));
    }

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
        IReadOnlyList<string> ruleMarkdowns) =>
        new()
        {
            TaskItem = taskItem,
            RuleMarkdowns = ruleMarkdowns.ToArray(),
            FlowResults = ruleMarkdowns.Select(ruleMarkdown => new RuleFlowWorkflowResult
            {
                TaskItem = taskItem,
                RuleMarkdown = ruleMarkdown,
                ReviewResult = new RuleReviewModels.RuleReviewWorkflowResult
                {
                    TaskItem = taskItem,
                    RuleMarkdown = ruleMarkdown,
                    Issues = [],
                    NoIssueConclusion = new RuleReviewModels.NoIssueConclusion
                    {
                        ReviewStrategy = "Reviewed the entry point.",
                        ScopeCoverage = "Inspected Program.cs.",
                        CrossScopeAnalysis = "No further tracing was required.",
                        WhyNoIssueWasFound = "No issue matched the rule.",
                    },
                    Verdict = new ReviewVerdict
                    {
                        Approved = true,
                        Message = "Review accepted.",
                    },
                    ReviewVerifierApproved = true,
                    ContinuedAfterVerifierRejectionLimit = false,
                    StoppedAfterMissingSubmissionLimit = false,
                    ShouldEnterReportAggregation = false,
                    ReviewAttempts = 1,
                    VerifierAttempts = 1,
                    RuleReviewAgentResetCount = 0,
                },
                ReportResult = null,
                EnteredReportAggregation = false,
                CompletionState = RuleFlowCompletionState.ApprovedNoIssue,
            }).ToArray(),
            HasAnyRuleFlows = ruleMarkdowns.Count > 0,
            AllRuleFlowsFinished = true,
            ApprovedCompletionCount = ruleMarkdowns.Count,
            DegradedCompletionCount = 0,
        };
}
