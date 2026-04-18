using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Concurrency;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Workflows.ReviewGroup;
using FluentResults;
using RuleReviewModels = CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Tests.Modules.ReviewAgentTeam;

[TestClass]
public sealed class ReviewAgentTeamWorkerTests
{
    [TestMethod]
    public async Task RunAsync_UsesSharedWorkerBudget_AndReturnsPreparationAndReviewResults()
    {
        using ReviewAgentTeamWorker worker = new(
            2,
            (repositoryRootPath, cancellationToken) => Task.FromResult(Result.Ok(CreateScanResult(CreateScanProject("scan-1", "ProjectOne")))),
            (repositoryRootPath, scanProject, cancellationToken) => Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject))),
            (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown))),
            new ReviewGroupWorkflow());

        Result<ReviewAgentTeamRunResult> result = await worker.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            ["- Rule A", "- Rule B"]);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, worker.MaxParallelAgents);
        Assert.AreEqual(1, result.Value.PreparationResult.ProjectPlanResults.Count);
        Assert.AreEqual(1, result.Value.ReviewStageResult.ProjectResults.Count);
        Assert.AreEqual(1, result.Value.ReviewStageResult.ProjectResults[0].ReviewGroupResults.Count);
    }

    [TestMethod]
    public void RunAsync_ThrowsAfterDispose()
    {
        ReviewAgentTeamWorker worker = new(
            2,
            (repositoryRootPath, cancellationToken) => Task.FromResult(Result.Ok(CreateScanResult(CreateScanProject("scan-1", "ProjectOne")))),
            (repositoryRootPath, scanProject, cancellationToken) => Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject))),
            (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown))),
            new ReviewGroupWorkflow());

        worker.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.RunAsync(@"Z:\GitHub\CodeSnifferDog", ["- Rule A"]).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void RunPreparationAsync_ThrowsAfterDispose()
    {
        ReviewAgentTeamWorker worker = new(
            2,
            (repositoryRootPath, cancellationToken) => Task.FromResult(Result.Ok(CreateScanResult(CreateScanProject("scan-1", "ProjectOne")))),
            (repositoryRootPath, scanProject, cancellationToken) => Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject))),
            (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown))),
            new ReviewGroupWorkflow());

        worker.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.RunPreparationAsync(@"Z:\GitHub\CodeSnifferDog").GetAwaiter().GetResult());
    }

    [TestMethod]
    public void RunReviewStageAsync_ThrowsAfterDispose()
    {
        ReviewAgentTeamWorker worker = new(
            2,
            (repositoryRootPath, cancellationToken) => Task.FromResult(Result.Ok(CreateScanResult(CreateScanProject("scan-1", "ProjectOne")))),
            (repositoryRootPath, scanProject, cancellationToken) => Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject))),
            (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown))),
            new ReviewGroupWorkflow());
        RepositoryPreparationWorkflowResult preparationResult = new()
        {
            ScanResult = CreateScanResult(CreateScanProject("scan-1", "ProjectOne")),
            ProjectPlanResults = [CreateProjectPlanResult(CreateScanProject("scan-1", "ProjectOne"))],
            ShouldEnterRuleReview = true,
        };

        worker.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.RunReviewStageAsync(@"Z:\GitHub\CodeSnifferDog", preparationResult, ["- Rule A"]).GetAwaiter().GetResult());
    }

    private static ScanWorkflowResult CreateScanResult(params StoredScanProject[] projects) =>
        new()
        {
            Projects = projects,
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
        };

    private static StoredScanProject CreateScanProject(string id, string name) =>
        new()
        {
            ScanProjectId = id,
            ProjectName = name,
            ProjectPath = $"src/{name}/{name}.csproj",
            ProjectType = ".csproj",
            Reason = $"Reason for {name}.",
        };

    private static ProjectPlanWorkflowResult CreateProjectPlanResult(StoredScanProject scanProject) =>
        new()
        {
            ScanProject = scanProject,
            TaskItems =
            [
                new StoredProjectPlanTaskItem
                {
                    ProjectPlanTaskItemId = $"task-{scanProject.ScanProjectId}",
                    Files =
                    [
                        new ProjectPlanFile
                        {
                            FilePath = $"src/{scanProject.ProjectName}/Program.cs",
                            TotalLines = 100,
                        },
                    ],
                },
            ],
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

    private static RuleFlowWorkflowResult CreateRuleFlowResult(StoredProjectPlanTaskItem taskItem, string ruleMarkdown) =>
        new()
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
        };
}
