using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Report;
using FluentResults;
using RuleReviewModels = CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Tests.Modules.ReviewAgentTeam;

[TestClass]
public sealed class ReviewAgentTeamWorkerTests
{
    private const string RepositoryRootPath = @"Z:\GitHub\CodeSnifferDog";
    private static readonly IReadOnlyList<ReviewAgentRuleDefinition> RuleDefinitions =
    [
        CreateRuleDefinition("rule-a", "- Rule A"),
        CreateRuleDefinition("rule-b", "- Rule B"),
    ];
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task AnalyzeAsync_UsesSharedWorkerBudget_AndCompletesSuccessfully()
    {
        ReviewAgentTeamFactory teamFactory = CreateTeamFactory();
        using ReviewAgentTeamWorker worker = CreateWorker(teamFactory);

        Result result = await worker.AnalyzeAsync(TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, worker.MaxParallelAgents);
        Assert.AreEqual(128_000L, worker.ExecutionOptions.ModelContextWindowTokens);
    }

    [TestMethod]
    public async Task GetRuleReportsAsync_ReturnsCurrentMarkdownReportsIndependentlyFromAnalyzeResult()
    {
        ReviewAgentTeamFactory teamFactory = CreateTeamFactory();
        using ReviewAgentTeamWorker worker = CreateWorker(teamFactory);

        Result analyzeResult = await worker.AnalyzeAsync(TestContext.CancellationToken);
        IReadOnlyList<ReviewAgentTeamRuleReport> ruleReports = await worker.GetRuleReportsAsync(TestContext.CancellationToken);

        Assert.IsTrue(analyzeResult.IsSuccess, string.Join(Environment.NewLine, analyzeResult.Errors.Select(error => error.Message)));
        Assert.HasCount(2, ruleReports);
        Assert.AreEqual("rule-a", ruleReports[0].RuleKey);
        Assert.Contains("# rule-a-report.md", ruleReports[0].MarkdownContent);
    }

    [TestMethod]
    public void AnalyzeAsync_ThrowsAfterDispose()
    {
        ReviewAgentTeamWorker worker = CreateWorker(CreateTeamFactory());

        worker.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.AnalyzeAsync(TestContext.CancellationToken).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void RunPreparationAsync_ThrowsAfterDispose()
    {
        ReviewAgentTeamWorker worker = CreateWorker(CreateTeamFactory());

        worker.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.RunPreparationAsync(TestContext.CancellationToken).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void RunReviewStageAsync_ThrowsAfterDispose()
    {
        ReviewAgentTeamWorker worker = CreateWorker(CreateTeamFactory());
        RepositoryPreparationWorkflowResult preparationResult = new()
        {
            ScanResult = CreateScanResult(CreateScanProject("scan-1", "ProjectOne")),
            ProjectPlanResults = [CreateProjectPlanResult(CreateScanProject("scan-1", "ProjectOne"))],
        };

        worker.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.RunReviewStageAsync(preparationResult, TestContext.CancellationToken).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void CreateWorker_ThrowsWhenMaxParallelAgentsIsNotPositive()
    {
        ReviewAgentTeamFactory teamFactory = CreateTeamFactory();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => teamFactory.CreateWorker(RepositoryRootPath, RuleDefinitions, new ReviewAgentTeamExecutionOptions
        {
            MaxParallelAgents = 0,
        }));
    }

    [TestMethod]
    public void CreateWorker_ThrowsWhenRepositoryRootPathIsBlank()
    {
        ReviewAgentTeamFactory teamFactory = CreateTeamFactory();

        Assert.ThrowsExactly<ArgumentException>(() => teamFactory.CreateWorker(" ", RuleDefinitions, CreateExecutionOptions()));
    }

    [TestMethod]
    public void CreateWorker_ThrowsWhenRuleDefinitionsIsNull()
    {
        ReviewAgentTeamFactory teamFactory = CreateTeamFactory();

        Assert.ThrowsExactly<ArgumentNullException>(() => teamFactory.CreateWorker(RepositoryRootPath, (IReadOnlyList<ReviewAgentRuleDefinition>)null!, CreateExecutionOptions()));
    }

    [TestMethod]
    public void CreateWorker_ThrowsWhenModelContextWindowTokensIsNotPositive()
    {
        ReviewAgentTeamFactory teamFactory = CreateTeamFactory();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => teamFactory.CreateWorker(RepositoryRootPath, RuleDefinitions, new ReviewAgentTeamExecutionOptions
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

        await worker.DisposeAsync().AsTask().WaitAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, cleanupCallCount);
    }

    private static ReviewAgentTeamFactory CreateTeamFactory(Action? cleanupAction = null) =>
        new(new ReviewAgentTeamDependencies
        {
            ScanWorkflowRunner = (repositoryRootPath, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateScanResult(CreateScanProject("scan-1", "ProjectOne")))),
            ProjectPlanWorkflowRunner = (repositoryRootPath, scanProject, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject))),
            RuleFlowWorkflowRunner = (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleKey, ruleMarkdown))),
            RuleReportIssueStore = new InMemoryRuleReportIssueStore(),
            AgentEventBus = null,
            CleanupAsync = _ =>
            {
                cleanupAction?.Invoke();
                return ValueTask.CompletedTask;
            },
        });

    private static ReviewAgentTeamWorker CreateWorker(ReviewAgentTeamFactory teamFactory) =>
        teamFactory.CreateWorker(RepositoryRootPath, RuleDefinitions, CreateExecutionOptions());

    private static ReviewAgentRuleDefinition CreateRuleDefinition(string ruleKey, string ruleMarkdown) =>
        new()
        {
            RuleKey = ruleKey,
            RuleMarkdown = ruleMarkdown,
        };

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
            ContinuedAfterVerifierRejectionLimit = false,
            PlanAttempts = 1,
            VerifierAttempts = 1,
            ProjectPlanAgentResetCount = 0,
        };

    private static RuleFlowWorkflowResult CreateRuleFlowResult(StoredProjectPlanTaskItem taskItem, string ruleKey, string _) =>
        new()
        {
            ReviewResult = new RuleReviewModels.RuleReviewWorkflowResult
            {
                TaskItem = taskItem,
                RuleKey = ruleKey,
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
                ContinuedAfterVerifierRejectionLimit = false,
                StoppedAfterMissingSubmissionLimit = false,
                ReviewAttempts = 1,
                VerifierAttempts = 1,
                RuleReviewAgentResetCount = 0,
            },
            ReportResult = null,
            CompletionState = RuleFlowCompletionState.ApprovedNoIssue,
        };
}
