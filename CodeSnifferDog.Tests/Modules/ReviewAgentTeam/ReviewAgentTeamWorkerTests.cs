using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using FluentResults;
using RuleReviewModels = CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Tests.Modules.ReviewAgentTeam;

[TestClass]
public sealed class ReviewAgentTeamWorkerTests
{
    private const string RepositoryRootPath = @"Z:\GitHub\CodeSnifferDog";
    private static readonly IReadOnlyList<string> RuleMarkdowns = ["- Rule A", "- Rule B"];

    [TestMethod]
    public async Task AnalyzeAsync_UsesSharedWorkerBudget_AndReturnsPreparationAndReviewResults()
    {
        ReviewAgentTeamFactory teamFactory = CreateTeamFactory();
        using ReviewAgentTeamWorker worker = CreateWorker(teamFactory);

        Result<ReviewAgentTeamRunResult> result = await worker.AnalyzeAsync();

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, worker.MaxParallelAgents);
        Assert.AreEqual(128_000L, worker.ExecutionOptions.ModelContextWindowTokens);
        Assert.HasCount(1, result.Value.PreparationResult.ProjectPlanResults);
        Assert.HasCount(1, result.Value.ReviewStageResult.ProjectResults);
        Assert.HasCount(1, result.Value.ReviewStageResult.ProjectResults[0].ReviewGroupResults);
    }

    [TestMethod]
    public void AnalyzeAsync_ThrowsAfterDispose()
    {
        ReviewAgentTeamWorker worker = CreateWorker(CreateTeamFactory());

        worker.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.AnalyzeAsync().GetAwaiter().GetResult());
    }

    [TestMethod]
    public void RunPreparationAsync_ThrowsAfterDispose()
    {
        ReviewAgentTeamWorker worker = CreateWorker(CreateTeamFactory());

        worker.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.RunPreparationAsync().GetAwaiter().GetResult());
    }

    [TestMethod]
    public void RunReviewStageAsync_ThrowsAfterDispose()
    {
        ReviewAgentTeamWorker worker = CreateWorker(CreateTeamFactory());
        RepositoryPreparationWorkflowResult preparationResult = new()
        {
            ScanResult = CreateScanResult(CreateScanProject("scan-1", "ProjectOne")),
            ProjectPlanResults = [CreateProjectPlanResult(CreateScanProject("scan-1", "ProjectOne"))],
            ShouldEnterRuleReview = true,
        };

        worker.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.RunReviewStageAsync(preparationResult).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void CreateWorker_ThrowsWhenMaxParallelAgentsIsNotPositive()
    {
        ReviewAgentTeamFactory teamFactory = CreateTeamFactory();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => teamFactory.CreateWorker(RepositoryRootPath, RuleMarkdowns, new ReviewAgentTeamExecutionOptions
        {
            MaxParallelAgents = 0,
        }));
    }

    [TestMethod]
    public void CreateWorker_ThrowsWhenRepositoryRootPathIsBlank()
    {
        ReviewAgentTeamFactory teamFactory = CreateTeamFactory();

        Assert.ThrowsExactly<ArgumentException>(() => teamFactory.CreateWorker(" ", RuleMarkdowns, CreateExecutionOptions()));
    }

    [TestMethod]
    public void CreateWorker_ThrowsWhenRuleMarkdownsIsNull()
    {
        ReviewAgentTeamFactory teamFactory = CreateTeamFactory();

        Assert.ThrowsExactly<ArgumentNullException>(() => teamFactory.CreateWorker(RepositoryRootPath, null!, CreateExecutionOptions()));
    }

    [TestMethod]
    public void CreateWorker_ThrowsWhenModelContextWindowTokensIsNotPositive()
    {
        ReviewAgentTeamFactory teamFactory = CreateTeamFactory();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => teamFactory.CreateWorker(RepositoryRootPath, RuleMarkdowns, new ReviewAgentTeamExecutionOptions
        {
            MaxParallelAgents = 2,
            ModelContextWindowTokens = 0,
        }));
    }

    [TestMethod]
    public void Dispose_CallsCleanupHookOnce()
    {
        int cleanupCallCount = 0;
        ReviewAgentTeamWorker worker = CreateWorker(CreateTeamFactory(() => cleanupCallCount++));

        worker.Dispose();
        worker.Dispose();

        Assert.AreEqual(1, cleanupCallCount);
    }

    [TestMethod]
    public async Task DisposeAsync_CallsCleanupHookOnce()
    {
        int cleanupCallCount = 0;
        await using ReviewAgentTeamWorker worker = CreateWorker(CreateTeamFactory(() => cleanupCallCount++));

        await worker.DisposeAsync();

        Assert.AreEqual(1, cleanupCallCount);
    }

    private static ReviewAgentTeamFactory CreateTeamFactory(Action? cleanupAction = null) =>
        new(new ReviewAgentTeamDependencies
        {
            ScanWorkflowRunner = (repositoryRootPath, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateScanResult(CreateScanProject("scan-1", "ProjectOne")))),
            ProjectPlanWorkflowRunner = (repositoryRootPath, scanProject, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject))),
            RuleFlowWorkflowRunner = (repositoryRootPath, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleMarkdown))),
            CleanupAsync = cancellationToken =>
            {
                cleanupAction?.Invoke();
                return ValueTask.CompletedTask;
            },
        });

    private static ReviewAgentTeamWorker CreateWorker(ReviewAgentTeamFactory teamFactory) =>
        teamFactory.CreateWorker(RepositoryRootPath, RuleMarkdowns, CreateExecutionOptions());

    private static ReviewAgentTeamExecutionOptions CreateExecutionOptions() => new()
    {
        MaxParallelAgents = 2,
    };

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
