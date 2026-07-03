using CodeSnifferDog.Agents.Report;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Workflows.Report;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Failures;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;
using WorkflowOptions = CodeSnifferDog.Models.Report.WorkflowOptions;
using WorkflowResult = CodeSnifferDog.Models.Report.WorkflowResult;
using RuleReviewStoredIssue = CodeSnifferDog.Models.RuleReview.StoredIssue;

namespace CodeSnifferDog.Tests.Workflows.Report;

[TestClass]
[DoNotParallelize]
public sealed class WorkflowTests
{
    private const string RuleFileName = "performance";

    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_ComputesDiffAgainstPreviousSnapshot()
    {
        InMemoryIssueStore reportIssueStore = await CreateSeededReportStoreAsync(TestContext.CancellationToken);
        Workflow workflow = CreateWorkflow(
            invocation => HandleAggregatorUpdateInvocation(invocation, reportIssueStore),
            HandleVerifierInvocation,
            reportIssueStore);

        Result<WorkflowResult> result = await workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            RuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsEmpty(result.Value.Diff.CreatedIssues);
        Assert.HasCount(1, result.Value.Diff.UpdatedIssues);
        Assert.IsEmpty(result.Value.Diff.DeletedIssues);
        Assert.AreEqual(Severity.High, result.Value.RepositoryIssues[0].Severity);
        Assert.AreEqual("Use a cached async path.", result.Value.RepositoryIssues[0].SuggestedFixDirection);
    }

    [TestMethod]
    public async Task RunAsync_ContinuesAfterVerifierRejectionLimit_AndPromotesWorkingReport()
    {
        InMemoryIssueStore reportIssueStore = new();
        Workflow workflow = CreateWorkflow(
            HandleAggregatorCreateInvocation,
            _ => CreateFunctionCallResponse(
                "verdict-reject",
                "SubmitReviewVerdict",
                new Dictionary<string, object?>
                {
                    ["Approved"] = false,
                    ["Message"] = "Separate the merged issues more conservatively.",
                }),
            reportIssueStore,
            new WorkflowOptions
            {
                MaxVerifierRejectionAttempts = 3,
            });

        Result<WorkflowResult> result = await workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            RuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        IReadOnlyList<ReportStoredIssue> latestSnapshot = await reportIssueStore.GetLatestSnapshotAsync(
            RuleScopeKeyFactory.CreateRuleReportKey(TestRepositoryPaths.RootPath, RuleFileName),
            TestContext.CancellationToken);
        IReadOnlyList<ReportStoredIssue> clearedWorkingIssues = await reportIssueStore.ListAsync(
            RuleScopeKeyFactory.CreateRuleFlowKey(TestRepositoryPaths.RootPath, "task-item-1", RuleFileName),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsTrue(result.Value.ContinuedAfterVerifierRejectionLimit);
        Assert.AreEqual(3, result.Value.AggregatorAttempts);
        Assert.AreEqual(3, result.Value.VerifierAttempts);
        Assert.HasCount(1, latestSnapshot);
        Assert.IsEmpty(clearedWorkingIssues);
    }

    [TestMethod]
    public async Task RunAsync_Fails_WhenVerifierDoesNotSubmitVerdict()
    {
        Workflow workflow = CreateWorkflow(
            HandleAggregatorCreateInvocation,
            _ => CreateAssistantResponse("No verdict submitted."));

        Result<WorkflowResult> result = await workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            RuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error =>
            error.Message.Contains("without submitting a verdict", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_IgnoresLateWrites_FromTimedOutAttempt()
    {
        TaskCompletionSource staleWriteObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int timedOutAttempts = 0;
        InMemoryIssueStore reportIssueStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        AsyncScriptedChatClient aggregatorChatClient = new(async (invocation, cancellationToken) =>
        {
            if (timedOutAttempts == 0)
            {
                timedOutAttempts++;
                Task backgroundWriteTask = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(150, CancellationToken.None);
                        await reportIssueStore.AddAsync(
                            RuleScopeKeyFactory.CreateRuleFlowKey(TestRepositoryPaths.RootPath, "task-item-1", RuleFileName),
                            new Issue
                            {
                                IssueType = "Performance",
                                Severity = "Low",
                                FileOrFunction = "Stale.cs",
                                RelevantCodePatternOrExpression = "stale path",
                                WhyThisIsAProblem = "Late write from timed out attempt.",
                                Confidence = "Low",
                                FollowUpFiles = "Stale.cs",
                                SuggestedFixDirection = "Ignore stale write.",
                                ReviewStrategy = "Timed out attempt.",
                                ScopeCoverage = "Stale scope.",
                                CrossScopeAnalysis = "No cross-scope inspection was required.",
                            },
                            CancellationToken.None);
                    }
                    finally
                    {
                        staleWriteObserved.TrySetResult();
                    }
                });
                _ = backgroundWriteTask;

                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return CreateFunctionCallResponse(
                "create-report-issue",
                "CreateRuleReportIssue",
                CreateIssueArguments(
                    "Performance",
                    "High",
                    "Program.cs",
                    "Repeated synchronous call",
                    "This blocks the request path.",
                    "High",
                    "Program.cs",
                    "Use a cached async path.",
                    "Inspected Program.cs.",
                    "No cross-scope inspection was required.",
                    "Reviewed the hot path first."));
        });
        ScriptedChatClient verifierChatClient = new(_ => CreateFunctionCallResponse(
            "verdict-approve",
            "SubmitReviewVerdict",
            new Dictionary<string, object?>
            {
                ["Approved"] = true,
                ["Message"] = "The current report diff is acceptable.",
            }));
        Workflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateAggregatorAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, aggregatorChatClient, reportIssueStore, verdictBuffer),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, currentFlowIssues, _) =>
                CreateVerifierAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, currentFlowIssues, verifierChatClient, reportIssueStore, verdictBuffer),
            reportIssueStore,
            verdictBuffer,
            new PromptAssetReader(),
            new WorkflowOptions
            {
                AgentRunTimeout = TimeSpan.FromMilliseconds(50),
                MaxConsecutiveRunFailures = 5,
            });

        Result<WorkflowResult> result = await workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            RuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        await staleWriteObserved.Task.WaitAsync(TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(1, timedOutAttempts);
        Assert.HasCount(1, result.Value.RepositoryIssues);
        Assert.AreEqual("Program.cs", result.Value.RepositoryIssues[0].FileOrFunction);
        Assert.IsFalse(result.Value.RepositoryIssues.Any(issue => issue.FileOrFunction == "Stale.cs"));
    }

    [TestMethod]
    public async Task RunAsync_PreservesWorkingReportAcrossVerifierRetry()
    {
        List<ChatInvocation> aggregatorInvocations = [];
        InMemoryIssueStore reportIssueStore = new();
        Workflow workflow = CreateWorkflow(
            invocation =>
            {
                aggregatorInvocations.Add(invocation);
                return HandleAggregatorCreateOrFixInvocation(invocation, reportIssueStore);
            },
            HandleVerifierRejectThenApproveInvocation,
            reportIssueStore);

        Result<WorkflowResult> result = await workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            RuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, result.Value.AggregatorAttempts);
        Assert.AreEqual(2, result.Value.VerifierAttempts);
        Assert.HasCount(1, result.Value.RepositoryIssues);
        Assert.IsTrue(aggregatorInvocations.Any(invocation => invocation.Messages.Any(message =>
            message.Role == ChatRole.User &&
            message.Text?.Contains("tighten the merged issue", StringComparison.OrdinalIgnoreCase) == true)));
        Assert.IsTrue(aggregatorInvocations.Any(invocation => invocation.Messages.Any(message =>
            message.Contents.OfType<FunctionResultContent>().Any())));
    }

    [TestMethod]
    public async Task RunAsync_PreservesCompactionArtifacts_AndCompletesWorkflow()
    {
        List<ChatInvocation> aggregatorInvocations = [];
        int aggregatorFailures = 0;
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        AgentCompactionOptions compactionOptions = CreateCompactionOptions(
            ReportAgentPromptAssets.ReportSummaryPrompt,
            summarizer);
        ScriptedChatClient aggregatorChatClient = new(invocation =>
        {
            aggregatorInvocations.Add(invocation);

            if (aggregatorFailures == 0)
            {
                aggregatorFailures++;
                throw new ModelInvocationException(
                    ModelInvocationFailureKind.ContextWindowExceeded,
                    "context too large");
            }

            return HandleAggregatorCreateInvocation(invocation);
        });
        ScriptedChatClient verifierChatClient = new(HandleVerifierInvocation);
        InMemoryIssueStore reportIssueStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();
        Workflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) => new ReportAggregatorAgentFactory(compactionOptions).Create(
                aggregatorChatClient,
                repositoryRootPath,
                ruleFileName,
                ruleMarkdown,
                taskItem,
                reportIssueStore,
                verdictBuffer),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, currentFlowIssues, _) => new ReportVerifierAgentFactory(compactionOptions).Create(
                verifierChatClient,
                repositoryRootPath,
                ruleFileName,
                ruleMarkdown,
                taskItem,
                currentFlowIssues,
                reportIssueStore,
                verdictBuffer),
            reportIssueStore,
            verdictBuffer,
            promptAssetReader);

        Result<WorkflowResult> result = await workflow.RunAsync(
            TestRepositoryPaths.RootPath,
            RuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsGreaterThan(0, summarizer.CallCount);
        Assert.IsGreaterThanOrEqualTo(2, aggregatorInvocations.Count);
        Assert.IsNotNull(summarizer.LastSummaryPrompt);
        Assert.Contains("Summarize the current Report Aggregation-stage work", summarizer.LastSummaryPrompt);

        ChatInvocation compactedInvocation = aggregatorInvocations.First(invocation =>
            invocation.Messages.Any(IsSummaryArtifactMessage));

        Assert.AreEqual(1, compactedInvocation.Messages.Count(message => IsSummaryArtifactMessage(message)));
    }

    [TestMethod]
    public async Task RunAsync_KeepsSameRuleParallelFlowsIsolated()
    {
        InMemoryIssueStore reportIssueStore = new();
        Workflow firstWorkflow = CreateWorkflow(
            HandleAggregatorCreateInvocation,
            HandleVerifierInvocation,
            reportIssueStore);
        Workflow secondWorkflow = CreateWorkflow(
            HandleAggregatorCreateInvocation,
            HandleVerifierInvocation,
            reportIssueStore);

        Task<Result<WorkflowResult>> firstRun = firstWorkflow.RunAsync(
            TestRepositoryPaths.RootPath,
            RuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);
        Task<Result<WorkflowResult>> secondRun = secondWorkflow.RunAsync(
            TestRepositoryPaths.RootPath,
            RuleFileName,
            "- Detect performance issues.",
            new StoredTaskItem
            {
                ProjectPlanTaskItemId = "task-item-2",
                Files =
                [
                    new PlanFile
                    {
                        FilePath = "CodeSnifferDog/CommonToolSet.cs",
                        TotalLines = 80,
                    },
                ],
            },
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        Result<WorkflowResult>[] results = await Task.WhenAll(firstRun, secondRun);

        Assert.IsTrue(results.All(result => result.IsSuccess));
        Assert.AreEqual("task-item-1", results[0].Value.TaskItem.ProjectPlanTaskItemId);
        Assert.AreEqual("task-item-2", results[1].Value.TaskItem.ProjectPlanTaskItemId);
    }

    [TestMethod]
    public async Task RunAsync_CleansWorkingReportAndVerdictBuffer_AfterSuccessfulCompletion()
    {
        InMemoryIssueStore reportIssueStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        Workflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateAggregatorAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, new ScriptedChatClient(HandleAggregatorCreateInvocation), reportIssueStore, verdictBuffer),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, currentFlowIssues, _) =>
                CreateVerifierAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, currentFlowIssues, new ScriptedChatClient(HandleVerifierInvocation), reportIssueStore, verdictBuffer),
            reportIssueStore,
            verdictBuffer,
            new PromptAssetReader());
        StoredTaskItem taskItem = CreateTaskItem();
        string repositoryRootPath = TestRepositoryPaths.RootPath;
        string ruleMarkdown = "- Detect performance issues.";

        Result<WorkflowResult> result = await workflow.RunAsync(
            repositoryRootPath,
            RuleFileName,
            ruleMarkdown,
            taskItem,
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        RuleFlowKey ruleFlowKey = RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, RuleFileName);
        string verdictScopeKey = RuleScopeKeyFactory.CreateReportVerdictScopeKey(ruleFlowKey);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsEmpty(await reportIssueStore.ListAsync(ruleFlowKey, TestContext.CancellationToken));
        Assert.IsEmpty((await reportIssueStore.GetLatestDiffAsync(ruleFlowKey, TestContext.CancellationToken)).CreatedIssues);
        Assert.IsNull(verdictBuffer.GetLatest(verdictScopeKey));
    }

    [TestMethod]
    public async Task RunAsync_CleansWorkingReportAndVerdictBuffer_AfterFailure()
    {
        InMemoryIssueStore reportIssueStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        Workflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateAggregatorAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, new ScriptedChatClient(HandleAggregatorCreateInvocation), reportIssueStore, verdictBuffer),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, currentFlowIssues, _) =>
                CreateVerifierAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, currentFlowIssues, new ScriptedChatClient(_ => CreateAssistantResponse("No verdict submitted.")), reportIssueStore, verdictBuffer),
            reportIssueStore,
            verdictBuffer,
            new PromptAssetReader());
        StoredTaskItem taskItem = CreateTaskItem();
        string repositoryRootPath = TestRepositoryPaths.RootPath;
        string ruleMarkdown = "- Detect performance issues.";

        Result<WorkflowResult> result = await workflow.RunAsync(
            repositoryRootPath,
            RuleFileName,
            ruleMarkdown,
            taskItem,
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        RuleFlowKey ruleFlowKey = RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, RuleFileName);
        string verdictScopeKey = RuleScopeKeyFactory.CreateReportVerdictScopeKey(ruleFlowKey);

        Assert.IsTrue(result.IsFailed);
        Assert.IsEmpty(await reportIssueStore.ListAsync(ruleFlowKey, TestContext.CancellationToken));
        Assert.IsEmpty((await reportIssueStore.GetLatestDiffAsync(ruleFlowKey, TestContext.CancellationToken)).CreatedIssues);
        Assert.IsNull(verdictBuffer.GetLatest(verdictScopeKey));
    }

    private static Workflow CreateWorkflow(
        Func<ChatInvocation, ChatResponse> aggregatorResponseFactory,
        Func<ChatInvocation, ChatResponse> verifierResponseFactory,
        InMemoryIssueStore? reportIssueStore = null,
        WorkflowOptions? options = null)
    {
        ScriptedChatClient aggregatorChatClient = new(aggregatorResponseFactory);
        ScriptedChatClient verifierChatClient = new(verifierResponseFactory);
        InMemoryIssueStore store = reportIssueStore ?? new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();

        return new Workflow(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateAggregatorAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, aggregatorChatClient, store, verdictBuffer),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, currentFlowIssues, _) =>
                CreateVerifierAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, currentFlowIssues, verifierChatClient, store, verdictBuffer),
            store,
            verdictBuffer,
            promptAssetReader,
            options);
    }

    private static AgentCreationResult CreateAggregatorAgent(
        string repositoryRootPath,
        string ruleFileName,
        string ruleMarkdown,
        StoredTaskItem taskItem,
        IChatClient chatClient,
        IIssueStore reportIssueStore,
        ReviewVerdictBuffer verdictBuffer) =>
        new ReportAggregatorAgentFactory(CreateCompactionOptions(ReportAgentPromptAssets.ReportSummaryPrompt))
            .Create(chatClient, repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, reportIssueStore, verdictBuffer);

    private static AgentCreationResult CreateVerifierAgent(
        string repositoryRootPath,
        string ruleFileName,
        string ruleMarkdown,
        StoredTaskItem taskItem,
        IReadOnlyList<RuleReviewStoredIssue> currentFlowIssues,
        IChatClient chatClient,
        IIssueStore reportIssueStore,
        ReviewVerdictBuffer verdictBuffer) =>
        new ReportVerifierAgentFactory(CreateCompactionOptions(ReportAgentPromptAssets.ReportSummaryPrompt))
            .Create(chatClient, repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, currentFlowIssues, reportIssueStore, verdictBuffer);

    private static async Task<InMemoryIssueStore> CreateSeededReportStoreAsync(CancellationToken cancellationToken)
    {
        InMemoryIssueStore store = new();
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(TestRepositoryPaths.RootPath, RuleFileName);
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(TestRepositoryPaths.RootPath, "seed-task-item", RuleFileName);
        await store.InitializeWorkingReportAsync(ruleReportKey, RuleFileName, ruleFlowKey, cancellationToken);
        await store.AddAsync(
            ruleFlowKey,
            new Issue
            {
                IssueType = "Performance",
                Severity = "Medium",
                FileOrFunction = "Program.cs",
                RelevantCodePatternOrExpression = "Repeated synchronous call",
                WhyThisIsAProblem = "This blocks the request path.",
                Confidence = "Medium",
                FollowUpFiles = "Program.cs",
                SuggestedFixDirection = "Investigate the hot path.",
                ReviewStrategy = "Reviewed the hot path first.",
                ScopeCoverage = "Inspected Program.cs.",
                CrossScopeAnalysis = "No cross-scope inspection was required.",
            },
            cancellationToken);
        await store.PromoteWorkingReportAsync(ruleReportKey, ruleFlowKey, cancellationToken);
        await store.ClearWorkingReportAsync(ruleFlowKey, cancellationToken);
        return store;
    }

    private static StoredTaskItem CreateTaskItem()
        =>
        new()
        {
            ProjectPlanTaskItemId = "task-item-1",
            Files =
            [
                new PlanFile
                {
                    FilePath = "CodeSnifferDog/Program.cs",
                    TotalLines = 120,
                },
            ],
        };

    private static IReadOnlyList<RuleReviewStoredIssue> CreateCurrentFlowIssues()
        =>
        [
            new RuleReviewStoredIssue
            {
                RuleReviewIssueId = "flow-issue-1",
                IssueType = "Performance",
                Severity = "High",
                FileOrFunction = "Program.cs",
                RelevantCodePatternOrExpression = "Repeated synchronous call",
                WhyThisIsAProblem = "This blocks the request path.",
                Confidence = "High",
                FollowUpFiles = "Program.cs",
                SuggestedFixDirection = "Use a cached async path.",
                ReviewStrategy = "Reviewed the hot path first.",
                ScopeCoverage = "Inspected Program.cs.",
                CrossScopeAnalysis = "No cross-scope inspection was required.",
            },
        ];

    private static ChatResponse HandleAggregatorCreateInvocation(ChatInvocation invocation)
    {
        if (HasFunctionResult(invocation.Messages, "create-report-issue"))
            return CreateAssistantResponse("Initial aggregation recorded.");

        return CreateFunctionCallResponse(
            "create-report-issue",
            "CreateRuleReportIssue",
            CreateIssueArguments(
                "Performance",
                "High",
                "Program.cs",
                "Repeated synchronous call",
                "This blocks the request path.",
                "High",
                "Program.cs",
                "Use a cached async path.",
                "Inspected Program.cs.",
                "No cross-scope inspection was required.",
                "Reviewed the hot path first."));
    }

    private static ChatResponse HandleAggregatorUpdateInvocation(
        ChatInvocation invocation,
        InMemoryIssueStore reportIssueStore)
    {
        if (HasFunctionResult(invocation.Messages, "update-report-issue"))
            return CreateAssistantResponse("Aggregation update recorded.");

        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(TestRepositoryPaths.RootPath, RuleFileName);
        IReadOnlyList<ReportStoredIssue> latestSnapshot =
            reportIssueStore.GetLatestSnapshotAsync(ruleReportKey, CancellationToken.None).AsTask().GetAwaiter().GetResult();

        return CreateFunctionCallResponse(
            "update-report-issue",
            "UpdateRuleReportIssue",
            CreateIssueArguments(
                "Performance",
                "High",
                "Program.cs",
                "Repeated synchronous call",
                "This blocks the request path.",
                "High",
                "Program.cs",
                "Use a cached async path.",
                "Inspected Program.cs.",
                "No cross-scope inspection was required.",
                "Reviewed the hot path first.",
                latestSnapshot[0].RuleReportIssueId));
    }

    private static ChatResponse HandleAggregatorCreateOrFixInvocation(
        ChatInvocation invocation,
        InMemoryIssueStore reportIssueStore)
    {
        if (HasFunctionResult(invocation.Messages, "update-report-issue"))
            return CreateAssistantResponse("Corrected aggregation recorded.");

        if (HasCorrectionInstruction(invocation.Messages))
        {
            RuleFlowKey ruleFlowKey =
                RuleScopeKeyFactory.CreateRuleFlowKey(TestRepositoryPaths.RootPath, "task-item-1", RuleFileName);
            IReadOnlyList<ReportStoredIssue> workingIssues =
                reportIssueStore.ListAsync(ruleFlowKey, CancellationToken.None).AsTask().GetAwaiter().GetResult();

            return CreateFunctionCallResponse(
                "update-report-issue",
                "UpdateRuleReportIssue",
                CreateIssueArguments(
                    "Performance",
                    "High",
                    "Program.cs",
                    "Repeated synchronous call",
                    "This blocks the request path.",
                    "High",
                    "Program.cs;Cache.cs",
                    "Use a cached async path and verify the cache boundary.",
                    "Inspected Program.cs and the related hot path.",
                    "Reviewed Cache.cs after the verifier asked for a tighter merge.",
                    "Reviewed the hot path first, then verified the cache call flow.",
                    workingIssues[0].RuleReportIssueId));
        }

        if (HasFunctionResult(invocation.Messages, "create-report-issue"))
            return CreateAssistantResponse("Initial aggregation recorded.");

        return CreateFunctionCallResponse(
            "create-report-issue",
            "CreateRuleReportIssue",
            CreateIssueArguments(
                "Performance",
                "High",
                "Program.cs",
                "Repeated synchronous call",
                "This blocks the request path.",
                "High",
                "Program.cs",
                "Use a cached async path.",
                "Inspected Program.cs.",
                "No cross-scope inspection was required.",
                "Reviewed the hot path first."));
    }

    private static ChatResponse HandleVerifierInvocation(ChatInvocation invocation)
    {
        if (HasFunctionResult(invocation.Messages, "verdict-approve"))
            return CreateAssistantResponse("Aggregation approved.");

        bool hasCreatedIssue = invocation.Messages.Any(message =>
            message.Role == ChatRole.User &&
            message.Text?.Contains("\"createdIssues\":[{", StringComparison.Ordinal) == true);
        bool hasUpdatedIssue = invocation.Messages.Any(message =>
            message.Role == ChatRole.User &&
            message.Text?.Contains("\"updatedIssues\":[{", StringComparison.Ordinal) == true);

        return CreateFunctionCallResponse(
            hasCreatedIssue || hasUpdatedIssue ? "verdict-approve" : "verdict-reject",
            "SubmitReviewVerdict",
            new Dictionary<string, object?>
            {
                ["Approved"] = hasCreatedIssue || hasUpdatedIssue,
                ["Message"] = hasCreatedIssue || hasUpdatedIssue
                    ? "The current report diff is acceptable."
                    : "Create or update the repository-level issue instead of leaving the diff empty.",
            });
    }

    private static ChatResponse HandleVerifierRejectThenApproveInvocation(ChatInvocation invocation)
    {
        if (HasFunctionResult(invocation.Messages, "verdict-approve"))
            return CreateAssistantResponse("Aggregation approved.");

        if (HasFunctionResult(invocation.Messages, "verdict-reject"))
            return CreateAssistantResponse("Aggregation requires one correction.");

        bool needsCorrection = invocation.Messages.Any(message =>
            message.Role == ChatRole.User &&
            message.Text?.Contains("\"createdIssues\":[{", StringComparison.Ordinal) == true &&
            message.Text?.Contains("Cache.cs", StringComparison.Ordinal) == false);

        return CreateFunctionCallResponse(
            needsCorrection ? "verdict-reject" : "verdict-approve",
            "SubmitReviewVerdict",
            new Dictionary<string, object?>
            {
                ["Approved"] = !needsCorrection,
                ["Message"] = needsCorrection
                    ? "Please tighten the merged issue by adding the cache follow-up and clarifying the cross-scope analysis."
                    : "The current report diff is acceptable.",
            });
    }

    private static Dictionary<string, object?> CreateIssueArguments(
        string issueType,
        string severity,
        string fileOrFunction,
        string relevantCodePatternOrExpression,
        string whyThisIsAProblem,
        string confidence,
        string followUpFiles,
        string suggestedFixDirection,
        string scopeCoverage,
        string crossScopeAnalysis,
        string reviewStrategy,
        string? ruleReportIssueId = null)
    {
        Dictionary<string, object?> arguments = new()
        {
            ["IssueType"] = issueType,
            ["Severity"] = severity,
            ["FileOrFunction"] = fileOrFunction,
            ["RelevantCodePatternOrExpression"] = relevantCodePatternOrExpression,
            ["WhyThisIsAProblem"] = whyThisIsAProblem,
            ["Confidence"] = confidence,
            ["FollowUpFiles"] = followUpFiles,
            ["SuggestedFixDirection"] = suggestedFixDirection,
            ["ScopeCoverage"] = scopeCoverage,
            ["CrossScopeAnalysis"] = crossScopeAnalysis,
            ["ReviewStrategy"] = reviewStrategy,
        };

        if (!string.IsNullOrWhiteSpace(ruleReportIssueId))
            arguments["RuleReportIssueId"] = ruleReportIssueId;

        return arguments;
    }

    private static AgentCompactionOptions CreateCompactionOptions(
        string summaryPromptAssetPath,
        ISummarizer? summarizer = null) =>
        new AgentOptionsFactory(
            new PromptAssetReader(),
            summarizer ?? new RecordingSummarizer("<summary>Current objective\nCompleted work\nNext steps</summary>"))
            .CreateFromPromptAsset(
                summaryPromptAssetPath,
                new CompactionOptions
                {
                    ModelContextWindowTokens = 100,
                });

    private static ChatResponse CreateAssistantResponse(string text)
        =>
        new(new ChatMessage(ChatRole.Assistant, text));

    private static ChatResponse CreateFunctionCallResponse(
        string callId,
        string functionName,
        IDictionary<string, object?> arguments) => new(new ChatMessage(
        ChatRole.Assistant,
        [new FunctionCallContent(callId, functionName, arguments)]))
        {
            FinishReason = new ChatFinishReason("tool_calls"),
        };

    private static bool HasCorrectionInstruction(IReadOnlyList<ChatMessage> messages)
        =>
        messages.Any(message =>
            message.Role == ChatRole.User &&
            message.Text?.Contains("tighten the merged issue", StringComparison.OrdinalIgnoreCase) == true);

    private static bool HasFunctionResult(IReadOnlyList<ChatMessage> messages, string callId)
        =>
        messages.SelectMany(message => message.Contents)
            .Where(static content => content is FunctionResultContent)
            .Any(content => string.Equals(GetCallId(content), callId, StringComparison.Ordinal));

    private static string? GetCallId(AIContent content)
        =>
        content.GetType().GetProperty("CallId")?.GetValue(content)?.ToString();

    private static bool IsSummaryArtifactMessage(ChatMessage message)
        =>
        string.Equals(
            message.AdditionalProperties?.GetValueOrDefault(CompactionArtifactMetadata.ArtifactKindKey)?.ToString(),
            CompactionArtifactMetadata.SummaryArtifactKind,
            StringComparison.Ordinal);

    private sealed class ScriptedChatClient(Func<ChatInvocation, ChatResponse> responseFactory) : IChatClient
    {
        private int _callIndex = -1;
        private readonly Func<ChatInvocation, ChatResponse> _responseFactory = responseFactory;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            int callIndex = Interlocked.Increment(ref _callIndex);
            ChatInvocation invocation = new([.. messages], options, callIndex);
            return Task.FromResult(_responseFactory(invocation));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatResponse response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

            foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
                yield return update;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class AsyncScriptedChatClient(Func<ChatInvocation, CancellationToken, Task<ChatResponse>> responseFactory) : IChatClient
    {
        private int _callIndex = -1;
        private readonly Func<ChatInvocation, CancellationToken, Task<ChatResponse>> _responseFactory = responseFactory;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            int callIndex = Interlocked.Increment(ref _callIndex);
            ChatInvocation invocation = new([.. messages], options, callIndex);
            return _responseFactory(invocation, cancellationToken);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatResponse response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

            foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
                yield return update;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed record ChatInvocation(
        IReadOnlyList<ChatMessage> Messages,
        ChatOptions? Options,
        int CallIndex);

    private sealed class RecordingSummarizer(string response) : ISummarizer
    {
        public int CallCount { get; private set; }

        public string? LastSummaryPrompt { get; private set; }

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            CompactionOptions options,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastSummaryPrompt = summaryPrompt;
            return ValueTask.FromResult(response);
        }
    }
}
