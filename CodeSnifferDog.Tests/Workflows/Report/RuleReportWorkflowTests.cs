using CodeSnifferDog.Agents.Report;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Workflows.Report;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Workflows.Report;

[TestClass]
[DoNotParallelize]
public sealed class RuleReportWorkflowTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_ComputesDiffAgainstPreviousSnapshot()
    {
        InMemoryRuleReportIssueStore reportIssueStore = await CreateSeededReportStoreAsync(TestContext.CancellationToken);
        RuleReportWorkflow workflow = CreateWorkflow(
            invocation => HandleAggregatorUpdateInvocation(invocation, reportIssueStore),
            HandleVerifierInvocation,
            reportIssueStore);

        Result<RuleReportWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            "- Detect performance issues.",
            CreateTaskItem(),
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsEmpty(result.Value.Diff.CreatedIssues);
        Assert.HasCount(1, result.Value.Diff.UpdatedIssues);
        Assert.IsEmpty(result.Value.Diff.DeletedIssues);
        Assert.AreEqual("Use a cached async path.", result.Value.RepositoryIssues[0].SuggestedFixDirection);
    }

    [TestMethod]
    public async Task RunAsync_ContinuesAfterVerifierRejectionLimit_AndPromotesWorkingReport()
    {
        InMemoryRuleReportIssueStore reportIssueStore = new();
        RuleReportWorkflow workflow = CreateWorkflow(
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
            new RuleReportWorkflowOptions
            {
                MaxVerifierRejectionAttempts = 3,
            });

        Result<RuleReportWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            "- Detect performance issues.",
            CreateTaskItem(),
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        IReadOnlyList<StoredRuleReportIssue> latestSnapshot = await reportIssueStore.GetLatestSnapshotAsync(
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\GitHub\CodeSnifferDog", "- Detect performance issues."),
            TestContext.CancellationToken);
        IReadOnlyList<StoredRuleReportIssue> clearedWorkingIssues = await reportIssueStore.ListAsync(
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\GitHub\CodeSnifferDog", "task-item-1", "- Detect performance issues."),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsFalse(result.Value.ReportVerifierApproved);
        Assert.IsTrue(result.Value.ContinuedAfterVerifierRejectionLimit);
        Assert.AreEqual(3, result.Value.AggregatorAttempts);
        Assert.AreEqual(3, result.Value.VerifierAttempts);
        Assert.HasCount(1, latestSnapshot);
        Assert.IsEmpty(clearedWorkingIssues);
    }

    [TestMethod]
    public async Task RunAsync_Fails_WhenVerifierDoesNotSubmitVerdict()
    {
        RuleReportWorkflow workflow = CreateWorkflow(
            HandleAggregatorCreateInvocation,
            _ => CreateAssistantResponse("No verdict submitted."));

        Result<RuleReportWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            "- Detect performance issues.",
            CreateTaskItem(),
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error =>
            error.Message.Contains("without submitting a verdict", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_PreservesWorkingReportAcrossVerifierRetry()
    {
        List<ChatInvocation> aggregatorInvocations = [];
        InMemoryRuleReportIssueStore reportIssueStore = new();
        RuleReportWorkflow workflow = CreateWorkflow(
            invocation =>
            {
                aggregatorInvocations.Add(invocation);
                return HandleAggregatorCreateOrFixInvocation(invocation, reportIssueStore);
            },
            HandleVerifierRejectThenApproveInvocation,
            reportIssueStore);

        Result<RuleReportWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
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
        OperationalContextAgentCompactionOptions compactionOptions = CreateCompactionOptions(
            ReportPromptAssetPaths.ReportSummaryPrompt,
            summarizer);
        ScriptedChatClient aggregatorChatClient = new(invocation =>
        {
            aggregatorInvocations.Add(invocation);

            if (aggregatorFailures == 0)
            {
                aggregatorFailures++;
                throw new OperationalContextModelInvocationException(
                    OperationalContextModelInvocationFailureKind.ContextWindowExceeded,
                    "context too large");
            }

            return HandleAggregatorCreateInvocation(invocation);
        });
        ScriptedChatClient verifierChatClient = new(HandleVerifierInvocation);
        InMemoryRuleReportIssueStore reportIssueStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();
        RuleReportWorkflow workflow = new(
            (repositoryRootPath, ruleMarkdown, taskItem) => new ReportAggregatorAgentFactory(compactionOptions).Create(
                aggregatorChatClient,
                repositoryRootPath,
                ruleMarkdown,
                taskItem,
                reportIssueStore,
                verdictBuffer),
            (repositoryRootPath, ruleMarkdown, taskItem, currentFlowIssues) => new ReportVerifierAgentFactory(compactionOptions).Create(
                verifierChatClient,
                repositoryRootPath,
                ruleMarkdown,
                taskItem,
                currentFlowIssues,
                reportIssueStore,
                verdictBuffer),
            reportIssueStore,
            verdictBuffer,
            promptAssetReader);

        Result<RuleReportWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            "- Detect performance issues.",
            CreateTaskItem(),
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsGreaterThan(0, summarizer.CallCount);
        Assert.IsGreaterThanOrEqualTo(2, aggregatorInvocations.Count);
        Assert.IsNotNull(summarizer.LastSummaryPrompt);
        StringAssert.Contains(summarizer.LastSummaryPrompt, "Summarize the current Report Aggregation-stage work");

        ChatInvocation compactedInvocation = aggregatorInvocations.First(invocation =>
            invocation.Messages.Any(IsSummaryArtifactMessage));

        Assert.AreEqual(1, compactedInvocation.Messages.Count(message => IsSummaryArtifactMessage(message)));
    }

    [TestMethod]
    public async Task RunAsync_KeepsSameRuleParallelFlowsIsolated()
    {
        InMemoryRuleReportIssueStore reportIssueStore = new();
        RuleReportWorkflow firstWorkflow = CreateWorkflow(
            HandleAggregatorCreateInvocation,
            HandleVerifierInvocation,
            reportIssueStore);
        RuleReportWorkflow secondWorkflow = CreateWorkflow(
            HandleAggregatorCreateInvocation,
            HandleVerifierInvocation,
            reportIssueStore);

        Task<Result<RuleReportWorkflowResult>> firstRun = firstWorkflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            "- Detect performance issues.",
            CreateTaskItem(),
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);
        Task<Result<RuleReportWorkflowResult>> secondRun = secondWorkflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            "- Detect performance issues.",
            new StoredProjectPlanTaskItem
            {
                ProjectPlanTaskItemId = "task-item-2",
                Files =
                [
                    new ProjectPlanFile
                    {
                        FilePath = "CodeSnifferDog/CommonToolSet.cs",
                        TotalLines = 80,
                    },
                ],
            },
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        Result<RuleReportWorkflowResult>[] results = await Task.WhenAll(firstRun, secondRun);

        Assert.IsTrue(results.All(result => result.IsSuccess));
        Assert.AreEqual("task-item-1", results[0].Value.TaskItem.ProjectPlanTaskItemId);
        Assert.AreEqual("task-item-2", results[1].Value.TaskItem.ProjectPlanTaskItemId);
    }

    [TestMethod]
    public async Task RunAsync_CleansWorkingReportAndVerdictBuffer_AfterSuccessfulCompletion()
    {
        InMemoryRuleReportIssueStore reportIssueStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        RuleReportWorkflow workflow = new(
            (repositoryRootPath, ruleMarkdown, taskItem) =>
                CreateAggregatorAgent(repositoryRootPath, ruleMarkdown, taskItem, new ScriptedChatClient(HandleAggregatorCreateInvocation), reportIssueStore, verdictBuffer),
            (repositoryRootPath, ruleMarkdown, taskItem, currentFlowIssues) =>
                CreateVerifierAgent(repositoryRootPath, ruleMarkdown, taskItem, currentFlowIssues, new ScriptedChatClient(HandleVerifierInvocation), reportIssueStore, verdictBuffer),
            reportIssueStore,
            verdictBuffer,
            new PromptAssetReader());
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        string repositoryRootPath = @"Z:\GitHub\CodeSnifferDog";
        string ruleMarkdown = "- Detect performance issues.";

        Result<RuleReportWorkflowResult> result = await workflow.RunAsync(
            repositoryRootPath,
            ruleMarkdown,
            taskItem,
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        RuleFlowKey ruleFlowKey = RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, ruleMarkdown);
        string verdictScopeKey = RuleScopeKeyFactory.CreateReportVerdictScopeKey(ruleFlowKey);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsEmpty(await reportIssueStore.ListAsync(ruleFlowKey, TestContext.CancellationToken));
        Assert.IsEmpty((await reportIssueStore.GetLatestDiffAsync(ruleFlowKey, TestContext.CancellationToken)).CreatedIssues);
        Assert.IsNull(verdictBuffer.GetLatest(verdictScopeKey));
    }

    [TestMethod]
    public async Task RunAsync_CleansWorkingReportAndVerdictBuffer_AfterFailure()
    {
        InMemoryRuleReportIssueStore reportIssueStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        RuleReportWorkflow workflow = new(
            (repositoryRootPath, ruleMarkdown, taskItem) =>
                CreateAggregatorAgent(repositoryRootPath, ruleMarkdown, taskItem, new ScriptedChatClient(HandleAggregatorCreateInvocation), reportIssueStore, verdictBuffer),
            (repositoryRootPath, ruleMarkdown, taskItem, currentFlowIssues) =>
                CreateVerifierAgent(repositoryRootPath, ruleMarkdown, taskItem, currentFlowIssues, new ScriptedChatClient(_ => CreateAssistantResponse("No verdict submitted.")), reportIssueStore, verdictBuffer),
            reportIssueStore,
            verdictBuffer,
            new PromptAssetReader());
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        string repositoryRootPath = @"Z:\GitHub\CodeSnifferDog";
        string ruleMarkdown = "- Detect performance issues.";

        Result<RuleReportWorkflowResult> result = await workflow.RunAsync(
            repositoryRootPath,
            ruleMarkdown,
            taskItem,
            CreateCurrentFlowIssues(),
            TestContext.CancellationToken);

        RuleFlowKey ruleFlowKey = RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, ruleMarkdown);
        string verdictScopeKey = RuleScopeKeyFactory.CreateReportVerdictScopeKey(ruleFlowKey);

        Assert.IsTrue(result.IsFailed);
        Assert.IsEmpty(await reportIssueStore.ListAsync(ruleFlowKey, TestContext.CancellationToken));
        Assert.IsEmpty((await reportIssueStore.GetLatestDiffAsync(ruleFlowKey, TestContext.CancellationToken)).CreatedIssues);
        Assert.IsNull(verdictBuffer.GetLatest(verdictScopeKey));
    }

    private static RuleReportWorkflow CreateWorkflow(
        Func<ChatInvocation, ChatResponse> aggregatorResponseFactory,
        Func<ChatInvocation, ChatResponse> verifierResponseFactory,
        InMemoryRuleReportIssueStore? reportIssueStore = null,
        RuleReportWorkflowOptions? options = null)
    {
        ScriptedChatClient aggregatorChatClient = new(aggregatorResponseFactory);
        ScriptedChatClient verifierChatClient = new(verifierResponseFactory);
        InMemoryRuleReportIssueStore store = reportIssueStore ?? new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();

        return new RuleReportWorkflow(
            (repositoryRootPath, ruleMarkdown, taskItem) =>
                CreateAggregatorAgent(repositoryRootPath, ruleMarkdown, taskItem, aggregatorChatClient, store, verdictBuffer),
            (repositoryRootPath, ruleMarkdown, taskItem, currentFlowIssues) =>
                CreateVerifierAgent(repositoryRootPath, ruleMarkdown, taskItem, currentFlowIssues, verifierChatClient, store, verdictBuffer),
            store,
            verdictBuffer,
            promptAssetReader,
            options);
    }

    private static AIAgent CreateAggregatorAgent(
        string repositoryRootPath,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IChatClient chatClient,
        IRuleReportIssueStore reportIssueStore,
        ReviewVerdictBuffer verdictBuffer) =>
        new ReportAggregatorAgentFactory(CreateCompactionOptions(ReportPromptAssetPaths.ReportSummaryPrompt))
            .Create(chatClient, repositoryRootPath, ruleMarkdown, taskItem, reportIssueStore, verdictBuffer);

    private static AIAgent CreateVerifierAgent(
        string repositoryRootPath,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues,
        IChatClient chatClient,
        IRuleReportIssueStore reportIssueStore,
        ReviewVerdictBuffer verdictBuffer) =>
        new ReportVerifierAgentFactory(CreateCompactionOptions(ReportPromptAssetPaths.ReportSummaryPrompt))
            .Create(chatClient, repositoryRootPath, ruleMarkdown, taskItem, currentFlowIssues, reportIssueStore, verdictBuffer);

    private static async Task<InMemoryRuleReportIssueStore> CreateSeededReportStoreAsync(CancellationToken cancellationToken)
    {
        InMemoryRuleReportIssueStore store = new();
        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\GitHub\CodeSnifferDog", "- Detect performance issues.");
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\GitHub\CodeSnifferDog", "seed-task-item", "- Detect performance issues.");
        await store.InitializeWorkingReportAsync(ruleReportKey, ruleFlowKey, cancellationToken);
        await store.AddAsync(
            ruleFlowKey,
            new RuleReviewIssue
            {
                IssueType = "Performance",
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

    private static StoredProjectPlanTaskItem CreateTaskItem()
        =>
        new()
        {
            ProjectPlanTaskItemId = "task-item-1",
            Files =
            [
                new ProjectPlanFile
                {
                    FilePath = "CodeSnifferDog/Program.cs",
                    TotalLines = 120,
                },
            ],
        };

    private static IReadOnlyList<StoredRuleReviewIssue> CreateCurrentFlowIssues()
        =>
        [
            new StoredRuleReviewIssue
            {
                RuleReviewIssueId = "flow-issue-1",
                IssueType = "Performance",
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
        InMemoryRuleReportIssueStore reportIssueStore)
    {
        if (HasFunctionResult(invocation.Messages, "update-report-issue"))
            return CreateAssistantResponse("Aggregation update recorded.");

        RuleReportKey ruleReportKey =
            RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\GitHub\CodeSnifferDog", "- Detect performance issues.");
        IReadOnlyList<StoredRuleReportIssue> latestSnapshot =
            reportIssueStore.GetLatestSnapshotAsync(ruleReportKey, CancellationToken.None).AsTask().GetAwaiter().GetResult();

        return CreateFunctionCallResponse(
            "update-report-issue",
            "UpdateRuleReportIssue",
            CreateIssueArguments(
                "Performance",
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
        InMemoryRuleReportIssueStore reportIssueStore)
    {
        if (HasFunctionResult(invocation.Messages, "update-report-issue"))
            return CreateAssistantResponse("Corrected aggregation recorded.");

        if (HasCorrectionInstruction(invocation.Messages))
        {
            RuleReportKey ruleReportKey =
                RuleScopeKeyFactory.CreateRuleReportKey(@"Z:\GitHub\CodeSnifferDog", "- Detect performance issues.");
            RuleFlowKey ruleFlowKey =
                RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\GitHub\CodeSnifferDog", "task-item-1", "- Detect performance issues.");
            IReadOnlyList<StoredRuleReportIssue> workingIssues =
                reportIssueStore.ListAsync(ruleFlowKey, CancellationToken.None).AsTask().GetAwaiter().GetResult();

            return CreateFunctionCallResponse(
                "update-report-issue",
                "UpdateRuleReportIssue",
                CreateIssueArguments(
                    "Performance",
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
            message.Text?.Contains("\"CreatedIssues\":[{", StringComparison.Ordinal) == true);
        bool hasUpdatedIssue = invocation.Messages.Any(message =>
            message.Role == ChatRole.User &&
            message.Text?.Contains("\"UpdatedIssues\":[{", StringComparison.Ordinal) == true);

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
            message.Text?.Contains("\"CreatedIssues\":[{", StringComparison.Ordinal) == true &&
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

    private static OperationalContextAgentCompactionOptions CreateCompactionOptions(
        string summaryPromptAssetPath,
        IOperationalContextCompactionSummarizer? summarizer = null) =>
        new OperationalContextAgentCompactionOptionsFactory(
            new PromptAssetReader(),
            summarizer ?? new RecordingSummarizer("<summary>Current objective\nCompleted work\nNext steps</summary>"))
            .CreateFromPromptAsset(
                summaryPromptAssetPath,
                new OperationalContextCompactionOptions
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
            message.AdditionalProperties?.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.ArtifactKindKey)?.ToString(),
            OperationalContextCompactionArtifactMetadata.SummaryArtifactKind,
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

    private sealed record ChatInvocation(
        IReadOnlyList<ChatMessage> Messages,
        ChatOptions? Options,
        int CallIndex);

    private sealed class RecordingSummarizer(string response) : IOperationalContextCompactionSummarizer
    {
        public int CallCount { get; private set; }

        public string? LastSummaryPrompt { get; private set; }

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastSummaryPrompt = summaryPrompt;
            return ValueTask.FromResult(response);
        }
    }
}
