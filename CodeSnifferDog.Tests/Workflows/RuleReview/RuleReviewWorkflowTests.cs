using CodeSnifferDog.Agents.RuleReview;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Workflows.RuleReview;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Workflows.RuleReview;

[TestClass]
[DoNotParallelize]
public sealed class RuleReviewWorkflowTests
{
    private const string PerformanceRuleFileName = "performance";
    private const string MemoryRuleFileName = "memory";

    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_CompletesIssueWorkflow_ThroughRealToolCalls()
    {
        RuleReviewWorkflow workflow = CreateWorkflow(
            HandleIssueReviewInvocation,
            HandleIssueVerifierInvocation);

        Result<RuleReviewWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            PerformanceRuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, result.Value.ReviewAttempts);
        Assert.AreEqual(2, result.Value.VerifierAttempts);
        Assert.AreEqual(0, result.Value.RuleReviewAgentResetCount);
        Assert.IsFalse(result.Value.ContinuedAfterVerifierRejectionLimit);
        Assert.IsFalse(result.Value.StoppedAfterMissingSubmissionLimit);
        Assert.HasCount(2, result.Value.Issues);
        Assert.IsNull(result.Value.NoIssueConclusion);
        Assert.IsTrue(result.Value.Verdict.Approved);
    }

    [TestMethod]
    public async Task RunAsync_CompletesNoIssueWorkflow_ThroughRealToolCalls()
    {
        RuleReviewWorkflow workflow = CreateWorkflow(
            HandleNoIssueReviewInvocation,
            HandleNoIssueVerifierInvocation);

        Result<RuleReviewWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            PerformanceRuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, result.Value.ReviewAttempts);
        Assert.AreEqual(2, result.Value.VerifierAttempts);
        Assert.IsFalse(result.Value.StoppedAfterMissingSubmissionLimit);
        Assert.IsNotNull(result.Value.NoIssueConclusion);
        Assert.HasCount(0, result.Value.Issues);
        Assert.IsTrue(result.Value.Verdict.Approved);
    }

    [TestMethod]
    public async Task RunAsync_ResetsRuleReviewAgentConversation_AfterRepeatedMissingSubmissions()
    {
        int emptyAttempts = 0;
        RuleReviewWorkflow workflow = CreateWorkflow(
            invocation =>
            {
                if (emptyAttempts < 3)
                {
                    emptyAttempts++;
                    return CreateAssistantResponse("No review result submitted yet.");
                }

                return CreateFunctionCallResponse(
                    "submit-no-issue",
                    "SubmitNoIssueConclusion",
                    new Dictionary<string, object?>
                    {
                        ["ReviewStrategy"] = "Reviewed the entry point.",
                        ["ScopeCoverage"] = "Inspected Program.cs.",
                        ["CrossScopeAnalysis"] = "No cross-scope inspection was required.",
                        ["WhyNoIssueWasFound"] = "No issue was found in the inspected path.",
                    });
            },
            _ => CreateFunctionCallResponse(
                "verdict-approve",
                "SubmitReviewVerdict",
                new Dictionary<string, object?>
                {
                    ["Approved"] = true,
                    ["Message"] = "The no-issue conclusion is acceptable.",
                }),
            new RuleReviewWorkflowOptions
            {
                MaxMissingSubmissionAttempts = 3,
                MaxRuleReviewAgentResets = 1,
            });

        Result<RuleReviewWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            PerformanceRuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(1, result.Value.RuleReviewAgentResetCount);
        Assert.AreEqual(4, result.Value.ReviewAttempts);
        Assert.AreEqual(1, result.Value.VerifierAttempts);
        Assert.IsFalse(result.Value.StoppedAfterMissingSubmissionLimit);
    }

    [TestMethod]
    public async Task RunAsync_IgnoresLateWrites_FromTimedOutAttempt()
    {
        TaskCompletionSource staleWriteObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int timedOutAttempts = 0;
        InMemoryRuleReviewIssueStore issueStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        AsyncScriptedChatClient reviewChatClient = new(async (invocation, cancellationToken) =>
        {
            if (timedOutAttempts == 0)
            {
                timedOutAttempts++;
                Task backgroundWriteTask = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(150, CancellationToken.None);
                        await issueStore.AddAsync(
                            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\GitHub\CodeSnifferDog", "task-item-1", PerformanceRuleFileName),
                            new RuleReviewIssue
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
                "create-issue",
                "CreateRuleReviewIssue",
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
                ["Message"] = "The current review result is acceptable for the next stage.",
            }));
        RuleReviewWorkflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateReviewAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, reviewChatClient, issueStore, verdictBuffer),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateVerifierAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, verifierChatClient, issueStore, verdictBuffer),
            issueStore,
            verdictBuffer,
            new PromptAssetReader(),
            new RuleReviewWorkflowOptions
            {
                AgentRunTimeout = TimeSpan.FromMilliseconds(50),
                MaxConsecutiveRunFailures = 5,
            });

        Result<RuleReviewWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            PerformanceRuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        await staleWriteObserved.Task.WaitAsync(TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(1, timedOutAttempts);
        Assert.HasCount(1, result.Value.Issues);
        Assert.AreEqual("Program.cs", result.Value.Issues[0].FileOrFunction);
    }

    [TestMethod]
    public async Task RunAsync_ContinuesAfterVerifierRejectionLimit_ForIssueResult()
    {
        RuleReviewWorkflow workflow = CreateWorkflow(
            invocation =>
            {
                if (HasFunctionResult(invocation.Messages, "create-issue"))
                    return CreateAssistantResponse("Issue recorded.");

                return CreateFunctionCallResponse(
                    "create-issue",
                    "CreateRuleReviewIssue",
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
            },
            _ => CreateFunctionCallResponse(
                "verdict-reject",
                "SubmitReviewVerdict",
                new Dictionary<string, object?>
                {
                    ["Approved"] = false,
                    ["Message"] = "Expand the dependency tracing before the review can be accepted.",
                }),
            new RuleReviewWorkflowOptions
            {
                MaxVerifierRejectionAttempts = 3,
            });

        Result<RuleReviewWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            PerformanceRuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(3, result.Value.ReviewAttempts);
        Assert.AreEqual(3, result.Value.VerifierAttempts);
        Assert.IsTrue(result.Value.ContinuedAfterVerifierRejectionLimit);
        Assert.IsFalse(result.Value.StoppedAfterMissingSubmissionLimit);
        Assert.IsNotEmpty(result.Value.Issues);
        Assert.IsFalse(result.Value.Verdict.Approved);
    }

    [TestMethod]
    public async Task RunAsync_StopsAtNoIssueResult_AfterVerifierRejectionLimit()
    {
        RuleReviewWorkflow workflow = CreateWorkflow(
            _ => CreateFunctionCallResponse(
                "submit-no-issue",
                "SubmitNoIssueConclusion",
                new Dictionary<string, object?>
                {
                    ["ReviewStrategy"] = "Reviewed the entry point.",
                    ["ScopeCoverage"] = "Inspected Program.cs.",
                    ["CrossScopeAnalysis"] = "No cross-scope inspection was required.",
                    ["WhyNoIssueWasFound"] = "No issue was found in the inspected path.",
                }),
            _ => CreateFunctionCallResponse(
                "verdict-reject",
                "SubmitReviewVerdict",
                new Dictionary<string, object?>
                {
                    ["Approved"] = false,
                    ["Message"] = "Inspect one more dependency before concluding no issue.",
                }),
            new RuleReviewWorkflowOptions
            {
                MaxVerifierRejectionAttempts = 3,
            });

        Result<RuleReviewWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            PerformanceRuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsTrue(result.Value.ContinuedAfterVerifierRejectionLimit);
        Assert.IsFalse(result.Value.StoppedAfterMissingSubmissionLimit);
        Assert.HasCount(0, result.Value.Issues);
        Assert.IsNotNull(result.Value.NoIssueConclusion);
    }

    [TestMethod]
    public async Task RunAsync_StopsAfterMissingSubmissionLimit()
    {
        RuleReviewWorkflow workflow = CreateWorkflow(
            _ => CreateAssistantResponse("Still no submission."),
            _ => CreateAssistantResponse("Verifier should not run."),
            new RuleReviewWorkflowOptions
            {
                MaxMissingSubmissionAttempts = 3,
                MaxRuleReviewAgentResets = 1,
            });

        Result<RuleReviewWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            PerformanceRuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsTrue(result.Value.StoppedAfterMissingSubmissionLimit);
        Assert.IsFalse(result.Value.ContinuedAfterVerifierRejectionLimit);
        Assert.HasCount(0, result.Value.Issues);
        Assert.IsNull(result.Value.NoIssueConclusion);
        Assert.IsFalse(result.Value.Verdict.Approved);
        Assert.IsTrue(result.Value.Verdict.Message.Contains("allowed reset limit", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RunAsync_Fails_WhenVerifierDoesNotSubmitVerdict()
    {
        RuleReviewWorkflow workflow = CreateWorkflow(
            _ => CreateFunctionCallResponse(
                "submit-no-issue",
                "SubmitNoIssueConclusion",
                new Dictionary<string, object?>
                {
                    ["ReviewStrategy"] = "Reviewed the entry point.",
                    ["ScopeCoverage"] = "Inspected Program.cs.",
                    ["CrossScopeAnalysis"] = "No cross-scope inspection was required.",
                    ["WhyNoIssueWasFound"] = "No issue was found in the inspected path.",
                }),
            _ => CreateAssistantResponse("No verdict submitted."));

        Result<RuleReviewWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            PerformanceRuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error =>
            error.Message.Contains("without submitting a verdict", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_PreservesCompactionArtifacts_AndCompletesWorkflow()
    {
        List<ChatInvocation> reviewInvocations = [];
        int reviewFailures = 0;
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextAgentCompactionOptions compactionOptions = CreateCompactionOptions(
            RuleReviewPromptAssetPaths.RuleReviewSummaryPrompt,
            summarizer);
        ScriptedChatClient reviewChatClient = new(invocation =>
        {
            reviewInvocations.Add(invocation);

            if (reviewFailures == 0)
            {
                reviewFailures++;
                throw new OperationalContextModelInvocationException(
                    OperationalContextModelInvocationFailureKind.ContextWindowExceeded,
                    "context too large");
            }

            return HandleIssueReviewInvocation(invocation);
        });
        ScriptedChatClient verifierChatClient = new(HandleIssueVerifierInvocation);
        InMemoryRuleReviewIssueStore issueStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();
        RuleReviewWorkflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) => new RuleReviewAgentFactory(compactionOptions).Create(
                reviewChatClient,
                repositoryRootPath,
                ruleFileName,
                ruleMarkdown,
                taskItem,
                issueStore,
                verdictBuffer),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) => new ReviewVerifierAgentFactory(compactionOptions).Create(
                verifierChatClient,
                repositoryRootPath,
                ruleFileName,
                ruleMarkdown,
                taskItem,
                issueStore,
                verdictBuffer),
            issueStore,
            verdictBuffer,
            promptAssetReader);

        Result<RuleReviewWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            PerformanceRuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsGreaterThan(0, summarizer.CallCount);
        Assert.IsGreaterThanOrEqualTo(2, reviewInvocations.Count);
        Assert.IsNotNull(summarizer.LastSummaryPrompt);
        Assert.Contains("Summarize the current Rule Review-stage work", summarizer.LastSummaryPrompt);

        ChatInvocation compactedInvocation = reviewInvocations.First(invocation =>
            invocation.Messages.Any(IsSummaryArtifactMessage));

        Assert.AreEqual(1, compactedInvocation.Messages.Count(message => IsSummaryArtifactMessage(message)));
        Assert.IsTrue(compactedInvocation.Messages.Any(message =>
            message.Text?.Contains("Operational summary checkpoint", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task SubmitReviewVerdictAsync_RejectsBlankMessage()
    {
        RuleReviewToolSet toolSet = new(
            new InMemoryRuleReviewIssueStore(),
            new ReviewVerdictBuffer(),
            RuleScopeKeyFactory.CreateRuleFlowKey(@"Z:\GitHub\CodeSnifferDog", "task-item-1", PerformanceRuleFileName));

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => toolSet.SubmitReviewVerdictAsync(
            new SubmitReviewVerdictArgs
            {
                Approved = false,
                Message = " ",
            },
            TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task RunAsync_ReturnsFailedResult_WhenAgentFactoryThrows()
    {
        RuleReviewWorkflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) => throw new InvalidOperationException("factory failed"),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) => throw new InvalidOperationException("verifier factory should not run"),
            new InMemoryRuleReviewIssueStore(),
            new ReviewVerdictBuffer(),
            new PromptAssetReader());

        Result<RuleReviewWorkflowResult> result = await workflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            PerformanceRuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error =>
            error.Message.Contains("Failed to create Rule Review Agent", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_KeepsParallelRuleFlowsIsolated_InSharedStoreAndVerdictBuffer()
    {
        InMemoryRuleReviewIssueStore sharedIssueStore = new();
        ReviewVerdictBuffer sharedVerdictBuffer = new();
        ScriptedChatClient reviewChatClient = new(HandleIssueReviewInvocation);
        ScriptedChatClient verifierChatClient = new(HandleIssueVerifierInvocation);
        PromptAssetReader promptAssetReader = new();

        RuleReviewWorkflow firstWorkflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateReviewAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, reviewChatClient, sharedIssueStore, sharedVerdictBuffer),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateVerifierAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, verifierChatClient, sharedIssueStore, sharedVerdictBuffer),
            sharedIssueStore,
            sharedVerdictBuffer,
            promptAssetReader);
        RuleReviewWorkflow secondWorkflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateReviewAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, reviewChatClient, sharedIssueStore, sharedVerdictBuffer),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateVerifierAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, verifierChatClient, sharedIssueStore, sharedVerdictBuffer),
            sharedIssueStore,
            sharedVerdictBuffer,
            promptAssetReader);

        Task<Result<RuleReviewWorkflowResult>> firstRun = firstWorkflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            PerformanceRuleFileName,
            "- Detect performance issues.",
            CreateTaskItem(),
            TestContext.CancellationToken);
        Task<Result<RuleReviewWorkflowResult>> secondRun = secondWorkflow.RunAsync(
            @"Z:\GitHub\CodeSnifferDog",
            MemoryRuleFileName,
            "- Detect memory issues.",
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
            TestContext.CancellationToken);

        Result<RuleReviewWorkflowResult>[] results = await Task.WhenAll(firstRun, secondRun);

        Assert.IsTrue(results.All(result => result.IsSuccess));
        Assert.AreEqual("task-item-1", results[0].Value.TaskItem.ProjectPlanTaskItemId);
        Assert.AreEqual("task-item-2", results[1].Value.TaskItem.ProjectPlanTaskItemId);
    }

    [TestMethod]
    public async Task RunAsync_CleansIssueStoreAndVerdictBuffer_AfterSuccessfulCompletion()
    {
        InMemoryRuleReviewIssueStore issueStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        RuleReviewWorkflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateReviewAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, new ScriptedChatClient(HandleIssueReviewInvocation), issueStore, verdictBuffer),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateVerifierAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, new ScriptedChatClient(HandleIssueVerifierInvocation), issueStore, verdictBuffer),
            issueStore,
            verdictBuffer,
            new PromptAssetReader());
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        string repositoryRootPath = @"Z:\GitHub\CodeSnifferDog";
        string ruleMarkdown = "- Detect performance issues.";

        Result<RuleReviewWorkflowResult> result = await workflow.RunAsync(
            repositoryRootPath,
            PerformanceRuleFileName,
            ruleMarkdown,
            taskItem,
            TestContext.CancellationToken);

        RuleFlowKey ruleFlowKey = RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, PerformanceRuleFileName);
        string verdictScopeKey = RuleScopeKeyFactory.CreateReviewVerdictScopeKey(ruleFlowKey);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsEmpty(await issueStore.ListAsync(ruleFlowKey, TestContext.CancellationToken));
        Assert.IsNull(await issueStore.GetNoIssueConclusionAsync(ruleFlowKey, TestContext.CancellationToken));
        Assert.IsNull(verdictBuffer.GetLatest(verdictScopeKey));
    }

    [TestMethod]
    public async Task RunAsync_CleansIssueStoreAndVerdictBuffer_AfterFailure()
    {
        InMemoryRuleReviewIssueStore issueStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        RuleReviewWorkflow workflow = new(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateReviewAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, new ScriptedChatClient(HandleIssueReviewInvocation), issueStore, verdictBuffer),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateVerifierAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, new ScriptedChatClient(_ => CreateAssistantResponse("No verdict submitted.")), issueStore, verdictBuffer),
            issueStore,
            verdictBuffer,
            new PromptAssetReader());
        StoredProjectPlanTaskItem taskItem = CreateTaskItem();
        string repositoryRootPath = @"Z:\GitHub\CodeSnifferDog";
        string ruleMarkdown = "- Detect performance issues.";

        Result<RuleReviewWorkflowResult> result = await workflow.RunAsync(
            repositoryRootPath,
            PerformanceRuleFileName,
            ruleMarkdown,
            taskItem,
            TestContext.CancellationToken);

        RuleFlowKey ruleFlowKey = RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, PerformanceRuleFileName);
        string verdictScopeKey = RuleScopeKeyFactory.CreateReviewVerdictScopeKey(ruleFlowKey);

        Assert.IsTrue(result.IsFailed);
        Assert.IsEmpty(await issueStore.ListAsync(ruleFlowKey, TestContext.CancellationToken));
        Assert.IsNull(await issueStore.GetNoIssueConclusionAsync(ruleFlowKey, TestContext.CancellationToken));
        Assert.IsNull(verdictBuffer.GetLatest(verdictScopeKey));
    }

    private static RuleReviewWorkflow CreateWorkflow(
        Func<ChatInvocation, ChatResponse> reviewResponseFactory,
        Func<ChatInvocation, ChatResponse> verifierResponseFactory,
        RuleReviewWorkflowOptions? options = null)
    {
        ScriptedChatClient reviewChatClient = new(reviewResponseFactory);
        ScriptedChatClient verifierChatClient = new(verifierResponseFactory);
        InMemoryRuleReviewIssueStore issueStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();

        return new RuleReviewWorkflow(
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateReviewAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, reviewChatClient, issueStore, verdictBuffer),
            (repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, _) =>
                CreateVerifierAgent(repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, verifierChatClient, issueStore, verdictBuffer),
            issueStore,
            verdictBuffer,
            promptAssetReader,
            options);
    }

    private static AIAgent CreateReviewAgent(
        string repositoryRootPath,
        string ruleFileName,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IChatClient chatClient,
        IRuleReviewIssueStore issueStore,
        ReviewVerdictBuffer verdictBuffer) =>
        new RuleReviewAgentFactory(CreateCompactionOptions(RuleReviewPromptAssetPaths.RuleReviewSummaryPrompt))
            .Create(chatClient, repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, issueStore, verdictBuffer);

    private static AIAgent CreateVerifierAgent(
        string repositoryRootPath,
        string ruleFileName,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IChatClient chatClient,
        IRuleReviewIssueStore issueStore,
        ReviewVerdictBuffer verdictBuffer) =>
        new ReviewVerifierAgentFactory(CreateCompactionOptions(RuleReviewPromptAssetPaths.RuleReviewSummaryPrompt))
            .Create(chatClient, repositoryRootPath, ruleFileName, ruleMarkdown, taskItem, issueStore, verdictBuffer);

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

    private static ChatResponse HandleIssueReviewInvocation(ChatInvocation invocation)
    {
        if (HasFunctionResult(invocation.Messages, "create-issue-secondary"))
            return CreateAssistantResponse("Secondary issue recorded.");

        if (HasCorrectionInstruction(invocation.Messages))
        {
            return CreateFunctionCallResponse(
                "create-issue-secondary",
                "CreateRuleReviewIssue",
                CreateIssueArguments(
                    "Performance",
                    "Medium",
                    "CommonToolSet.cs",
                    "Repeated process launch",
                    "This adds unnecessary overhead on a repeated path.",
                    "Medium",
                    "Program.cs, CommonToolSet.cs",
                    "Reduce repeated process creation.",
                    "Inspected Program.cs and CommonToolSet.cs.",
                    "Followed the call path from Program.cs into CommonToolSet.cs.",
                    "Started from Program.cs and traced the helper dependency."));
        }

        if (HasFunctionResult(invocation.Messages, "create-issue-primary"))
            return CreateAssistantResponse("Primary issue recorded.");

        return CreateFunctionCallResponse(
            "create-issue-primary",
            "CreateRuleReviewIssue",
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

    private static ChatResponse HandleIssueVerifierInvocation(ChatInvocation invocation)
    {
        if (HasFunctionResult(invocation.Messages, "verdict-approve"))
            return CreateAssistantResponse("Review approved.");

        if (HasFunctionResult(invocation.Messages, "verdict-reject"))
            return CreateAssistantResponse("Review requires one correction.");

        bool hasSecondaryIssue = invocation.Messages.Any(message =>
            message.Role == ChatRole.User &&
            message.Text?.Contains("CommonToolSet.cs", StringComparison.Ordinal) == true);

        return CreateFunctionCallResponse(
            hasSecondaryIssue ? "verdict-approve" : "verdict-reject",
            "SubmitReviewVerdict",
            new Dictionary<string, object?>
            {
                ["Approved"] = hasSecondaryIssue,
                ["Message"] = hasSecondaryIssue
                    ? "The current review result is acceptable for the next stage."
                    : "Expand the cross-scope tracing and include the related helper path before the review can be accepted.",
            });
    }

    private static ChatResponse HandleNoIssueReviewInvocation(ChatInvocation invocation)
    {
        if (HasCorrectionInstruction(invocation.Messages))
        {
            return CreateFunctionCallResponse(
                "submit-no-issue-updated",
                "SubmitNoIssueConclusion",
                new Dictionary<string, object?>
                {
                    ["ReviewStrategy"] = "Reviewed Program.cs and then traced the helper path.",
                    ["ScopeCoverage"] = "Inspected Program.cs and CommonToolSet.cs. Coverage is sufficient for this scope.",
                    ["CrossScopeAnalysis"] = "Cross-scope inspection followed Program.cs into CommonToolSet.cs because the helper path was relevant.",
                    ["WhyNoIssueWasFound"] = "The traced helper path did not reveal a rule violation.",
                });
        }

        return CreateFunctionCallResponse(
            "submit-no-issue-initial",
            "SubmitNoIssueConclusion",
            new Dictionary<string, object?>
            {
                ["ReviewStrategy"] = "Reviewed Program.cs only.",
                ["ScopeCoverage"] = "Inspected Program.cs.",
                ["CrossScopeAnalysis"] = "No cross-scope inspection was performed.",
                ["WhyNoIssueWasFound"] = "No issue was found in the inspected file.",
            });
    }

    private static ChatResponse HandleNoIssueVerifierInvocation(ChatInvocation invocation)
    {
        if (HasFunctionResult(invocation.Messages, "verdict-approve"))
            return CreateAssistantResponse("No-issue conclusion approved.");

        if (HasFunctionResult(invocation.Messages, "verdict-reject"))
            return CreateAssistantResponse("No-issue conclusion requires one correction.");

        bool hasCrossScopeInspection = invocation.Messages.Any(message =>
            message.Role == ChatRole.User &&
            message.Text?.Contains("CommonToolSet.cs", StringComparison.Ordinal) == true);

        return CreateFunctionCallResponse(
            hasCrossScopeInspection ? "verdict-approve" : "verdict-reject",
            "SubmitReviewVerdict",
            new Dictionary<string, object?>
            {
                ["Approved"] = hasCrossScopeInspection,
                ["Message"] = hasCrossScopeInspection
                    ? "The current no-issue conclusion is acceptable."
                    : "Inspect the relevant helper dependency before concluding that no issue exists.",
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
        string reviewStrategy)
        =>
        new()
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
            (message.Text?.Contains("cross-scope", StringComparison.OrdinalIgnoreCase) == true ||
             message.Text?.Contains("helper dependency", StringComparison.OrdinalIgnoreCase) == true));

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
