using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Tools.Report;
using FluentResults;
using RuleReviewModels = CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.ReviewAgentTeam.Runtime;
using PreparationWorkflowResult = CodeSnifferDog.Models.Preparation.WorkflowResult;
using ProjectPlanWorkflowResult = CodeSnifferDog.Models.ProjectPlan.WorkflowResult;
using ReportWorkflowResult = CodeSnifferDog.Models.Report.WorkflowResult;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Tests.Modules.ReviewAgentTeam;

[TestClass]
public sealed class WorkerTests
{
    private static readonly string RepositoryRootPath = TestRepositoryPaths.RootPath;
    private static readonly IReadOnlyList<RuleDefinition> RuleDefinitions =
    [
        CreateRuleDefinition("rule-a", "- Rule A"),
        CreateRuleDefinition("rule-b", "- Rule B"),
    ];
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task AnalyzeAsync_UsesSharedWorkerBudget_AndCompletesSuccessfully()
    {
        Factory teamFactory = CreateTeamFactory();
        using Worker worker = CreateWorker(teamFactory);

        Result result = await worker.AnalyzeAsync(TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, worker.MaxParallelAgents);
        Assert.AreEqual(128_000L, worker.ExecutionOptions.ModelContextWindowTokens);
    }

    [TestMethod]
    public async Task GetRuleReportsAsync_ReturnsCurrentMarkdownReportsIndependentlyFromAnalyzeResult()
    {
        Factory teamFactory = CreateTeamFactory();
        using Worker worker = CreateWorker(teamFactory);

        Result analyzeResult = await worker.AnalyzeAsync(TestContext.CancellationToken);
        IReadOnlyList<RuleReport> ruleReports = await worker.GetRuleReportsAsync(TestContext.CancellationToken);

        Assert.IsTrue(analyzeResult.IsSuccess, string.Join(Environment.NewLine, analyzeResult.Errors.Select(error => error.Message)));
        Assert.HasCount(2, ruleReports);
        Assert.AreEqual("rule-a", ruleReports[0].RuleKey);
        Assert.Contains("# rule-a-report.md", ruleReports[0].MarkdownContent);
    }

    [TestMethod]
    public async Task AnalyzeDetailedAsync_ReportsFindings_WhenReviewStageFailsButAnotherRuleProducedReport()
    {
        InMemoryIssueStore ruleReportIssueStore = new();
        await SeedRuleReportSnapshotAsync(ruleReportIssueStore, "rule-a", TestContext.CancellationToken);
        Factory teamFactory = new(new Dependencies
        {
            ScanWorkflowRunner = (repositoryRootPath, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateScanResult(CreateScanProject("scan-1", "ProjectOne")))),
            ProjectPlanWorkflowRunner = (repositoryRootPath, scanProject, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject))),
            RuleFlowWorkflowRunner = (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
            {
                if (ruleKey == "rule-b")
                    return Task.FromResult(Result.Fail<RuleFlowWorkflowResult>("rule-b flow failed."));

                return Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleKey, ruleMarkdown, CompletionState.ApprovedWithReport)));
            },
            RuleReportIssueStore = ruleReportIssueStore,
            AgentEventBus = null,
        });
        using Worker worker = CreateWorker(teamFactory);

        AnalysisResult analysisResult = await worker.AnalyzeDetailedAsync(TestContext.CancellationToken);

        Assert.IsTrue(analysisResult.PreparationSucceeded);
        Assert.IsFalse(analysisResult.ReviewStageSucceeded);
        Assert.IsTrue(analysisResult.HasAnyFindings);
        Assert.IsFalse(analysisResult.AllRuleFlowsSucceeded);
        Assert.IsTrue(analysisResult.ExecutionErrors.Any(error => error.Contains("rule-b flow failed.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AnalyzeAsync_Succeeds_WhenReviewStageFailsButFindingsExist()
    {
        InMemoryIssueStore ruleReportIssueStore = new();
        await SeedRuleReportSnapshotAsync(ruleReportIssueStore, "rule-a", TestContext.CancellationToken);
        Factory teamFactory = new(new Dependencies
        {
            ScanWorkflowRunner = (repositoryRootPath, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateScanResult(CreateScanProject("scan-1", "ProjectOne")))),
            ProjectPlanWorkflowRunner = (repositoryRootPath, scanProject, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject))),
            RuleFlowWorkflowRunner = (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
            {
                if (ruleKey == "rule-b")
                    return Task.FromResult(Result.Fail<RuleFlowWorkflowResult>("rule-b flow failed."));

                return Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleKey, ruleMarkdown, CompletionState.ApprovedWithReport)));
            },
            RuleReportIssueStore = ruleReportIssueStore,
            AgentEventBus = null,
        });
        using Worker worker = CreateWorker(teamFactory);

        Result analyzeResult = await worker.AnalyzeAsync(TestContext.CancellationToken);

        Assert.IsTrue(analyzeResult.IsSuccess, string.Join(Environment.NewLine, analyzeResult.Errors.Select(error => error.Message)));
    }

    [TestMethod]
    public async Task AnalyzeDetailedAsync_MarksAllRuleFlowsSucceededFalse_WhenAFlowDegradedWithoutFindings()
    {
        Factory teamFactory = new(new Dependencies
        {
            ScanWorkflowRunner = (repositoryRootPath, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateScanResult(CreateScanProject("scan-1", "ProjectOne")))),
            ProjectPlanWorkflowRunner = (repositoryRootPath, scanProject, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject))),
            RuleFlowWorkflowRunner = (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateRuleFlowResult(
                    taskItem,
                    ruleKey,
                    ruleMarkdown,
                    ruleKey == "rule-b" ? CompletionState.DegradedNoIssue : CompletionState.ApprovedNoIssue))),
            RuleReportIssueStore = new InMemoryIssueStore(),
            AgentEventBus = null,
        });
        using Worker worker = CreateWorker(teamFactory);

        AnalysisResult analysisResult = await worker.AnalyzeDetailedAsync(TestContext.CancellationToken);

        Assert.IsTrue(analysisResult.PreparationSucceeded);
        Assert.IsTrue(analysisResult.ReviewStageSucceeded);
        Assert.IsFalse(analysisResult.HasAnyFindings);
        Assert.IsFalse(analysisResult.AllRuleFlowsSucceeded);
        Assert.IsEmpty(analysisResult.ExecutionErrors);
    }

    [TestMethod]
    public async Task AnalyzeAsync_Fails_WhenNoFindingsAndAFlowDegraded()
    {
        Factory teamFactory = new(new Dependencies
        {
            ScanWorkflowRunner = (repositoryRootPath, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateScanResult(CreateScanProject("scan-1", "ProjectOne")))),
            ProjectPlanWorkflowRunner = (repositoryRootPath, scanProject, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject))),
            RuleFlowWorkflowRunner = (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateRuleFlowResult(
                    taskItem,
                    ruleKey,
                    ruleMarkdown,
                    ruleKey == "rule-b" ? CompletionState.DegradedNoIssue : CompletionState.ApprovedNoIssue))),
            RuleReportIssueStore = new InMemoryIssueStore(),
            AgentEventBus = null,
        });
        using Worker worker = CreateWorker(teamFactory);

        Result analyzeResult = await worker.AnalyzeAsync(TestContext.CancellationToken);

        Assert.IsTrue(analyzeResult.IsFailed);
        Assert.IsTrue(analyzeResult.Errors.Any(error =>
            error.Message.Contains("did not finish successfully", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AnalyzeAsync_ThrowsAfterDispose()
    {
        Worker worker = CreateWorker(CreateTeamFactory());

        worker.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.AnalyzeAsync(TestContext.CancellationToken).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void RunPreparationAsync_ThrowsAfterDispose()
    {
        Worker worker = CreateWorker(CreateTeamFactory());

        worker.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.RunPreparationAsync(TestContext.CancellationToken).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void RunReviewStageAsync_ThrowsAfterDispose()
    {
        Worker worker = CreateWorker(CreateTeamFactory());
        PreparationWorkflowResult preparationResult = new()
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
        Factory teamFactory = CreateTeamFactory();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => teamFactory.CreateWorker(RepositoryRootPath, RuleDefinitions, new ExecutionOptions
        {
            MaxParallelAgents = 0,
        }));
    }

    [TestMethod]
    public void CreateWorker_ThrowsWhenRepositoryRootPathIsBlank()
    {
        Factory teamFactory = CreateTeamFactory();

        Assert.ThrowsExactly<ArgumentException>(() => teamFactory.CreateWorker(" ", RuleDefinitions, CreateExecutionOptions()));
    }

    [TestMethod]
    public void CreateWorker_ThrowsWhenRuleDefinitionsIsNull()
    {
        Factory teamFactory = CreateTeamFactory();

        Assert.ThrowsExactly<ArgumentNullException>(() => teamFactory.CreateWorker(RepositoryRootPath, (IReadOnlyList<RuleDefinition>)null!, CreateExecutionOptions()));
    }

    [TestMethod]
    public void CreateWorker_ThrowsWhenModelContextWindowTokensIsNotPositive()
    {
        Factory teamFactory = CreateTeamFactory();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => teamFactory.CreateWorker(RepositoryRootPath, RuleDefinitions, new ExecutionOptions
        {
            MaxParallelAgents = 2,
            ModelContextWindowTokens = 0,
        }));
    }

    [TestMethod]
    public void Dispose_CallsCleanupHookOnce()
    {
        int cleanupCallCount = 0;
        Worker worker = CreateWorker(CreateTeamFactory(() => cleanupCallCount++));

        worker.Dispose();
        worker.Dispose();

        Assert.AreEqual(1, cleanupCallCount);
    }

    [TestMethod]
    public async Task DisposeAsync_CallsCleanupHookOnce()
    {
        int cleanupCallCount = 0;
        await using Worker worker = CreateWorker(CreateTeamFactory(() => cleanupCallCount++));

        await worker.DisposeAsync().AsTask().WaitAsync(TestContext.CancellationToken);

        Assert.AreEqual(1, cleanupCallCount);
    }

    private static Factory CreateTeamFactory(Action? cleanupAction = null) =>
        new(new Dependencies
        {
            ScanWorkflowRunner = (repositoryRootPath, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateScanResult(CreateScanProject("scan-1", "ProjectOne")))),
            ProjectPlanWorkflowRunner = (repositoryRootPath, scanProject, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateProjectPlanResult(scanProject))),
            RuleFlowWorkflowRunner = (repositoryRootPath, ruleKey, ruleMarkdown, taskItem, cancellationToken) =>
                Task.FromResult(Result.Ok(CreateRuleFlowResult(taskItem, ruleKey, ruleMarkdown))),
            RuleReportIssueStore = new InMemoryIssueStore(),
            AgentEventBus = null,
            CleanupAsync = _ =>
            {
                cleanupAction?.Invoke();
                return ValueTask.CompletedTask;
            },
        });

    private static Worker CreateWorker(Factory teamFactory) =>
        teamFactory.CreateWorker(RepositoryRootPath, RuleDefinitions, CreateExecutionOptions());

    private static RuleDefinition CreateRuleDefinition(string ruleKey, string ruleMarkdown) =>
        new()
        {
            RuleKey = ruleKey,
            RuleMarkdown = ruleMarkdown,
        };

    private static ExecutionOptions CreateExecutionOptions() => new()
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
                new StoredTaskItem
                {
                    ProjectPlanTaskItemId = $"task-{scanProject.ScanProjectId}",
                    Files =
                    [
                        new PlanFile
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

    private static RuleFlowWorkflowResult CreateRuleFlowResult(
        StoredTaskItem taskItem,
        string ruleKey,
        string _,
        CompletionState completionState = CompletionState.ApprovedNoIssue)
    {
        bool approved = completionState is CompletionState.ApprovedNoIssue or CompletionState.ApprovedWithReport;
        bool entersReportAggregation = completionState is CompletionState.ApprovedWithReport or CompletionState.DegradedWithReport;
        bool hasNoIssueConclusion = completionState is CompletionState.ApprovedNoIssue or CompletionState.DegradedNoIssue;

        return new RuleFlowWorkflowResult
        {
            ReviewResult = new RuleReviewModels.WorkflowResult
            {
                TaskItem = taskItem,
                RuleKey = ruleKey,
                Issues = entersReportAggregation ? [CreateReviewIssue()] : [],
                NoIssueConclusion = hasNoIssueConclusion ? new RuleReviewModels.NoIssueConclusion
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
                ContinuedAfterVerifierRejectionLimit = !approved && entersReportAggregation,
                StoppedAfterMissingSubmissionLimit = completionState == CompletionState.DegradedMissingSubmission,
                ReviewAttempts = 1,
                VerifierAttempts = 1,
                RuleReviewAgentResetCount = 0,
            },
            ReportResult = entersReportAggregation ? new ReportWorkflowResult
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

    private static async Task SeedRuleReportSnapshotAsync(
        InMemoryIssueStore ruleReportIssueStore,
        string ruleKey,
        CancellationToken cancellationToken)
    {
        RuleReportKey ruleReportKey = RuleScopeKeyFactory.CreateRuleReportKey(RepositoryRootPath, ruleKey);
        RuleFlowKey ruleFlowKey = RuleScopeKeyFactory.CreateRuleFlowKey(RepositoryRootPath, "task-snapshot", ruleKey);
        await ruleReportIssueStore.InitializeWorkingReportAsync(ruleReportKey, ruleKey, ruleFlowKey, cancellationToken);
        await ruleReportIssueStore.AddAsync(
            ruleFlowKey,
            new RuleReviewModels.Issue
            {
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
            },
            cancellationToken);
        await ruleReportIssueStore.PromoteWorkingReportAsync(ruleReportKey, ruleFlowKey, cancellationToken);
        await ruleReportIssueStore.ClearWorkingReportAsync(ruleFlowKey, cancellationToken);
    }
}
