using CodeSnifferDog.Agents.Scan;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.Scan;
using CodeSnifferDog.Workflows.Scan;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Workflows.Scan;

[TestClass]
[DoNotParallelize]
public sealed class ScanWorkflowTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_CompletesScanWorkflow_ThroughRealToolCalls()
    {
        ScanWorkflow workflow = CreateWorkflow(
            HandleScanInvocation,
            HandleVerifierInvocation);

        var result = await workflow.RunAsync(@"Z:\GitHub\CodeSnifferDog", TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, result.Value.ScanAttempts);
        Assert.AreEqual(2, result.Value.VerifierAttempts);
        Assert.AreEqual(0, result.Value.ScanAgentResetCount);
        Assert.IsTrue(result.Value.ScanVerifierApproved);
        Assert.IsFalse(result.Value.ContinuedAfterVerifierRejectionLimit);
        Assert.IsTrue(result.Value.ShouldEnterProjectPlanning);
        Assert.IsTrue(result.Value.Verdict.Approved);
        Assert.HasCount(2, result.Value.Projects);
        Assert.AreEqual("CodeSnifferDog", result.Value.Projects[0].ProjectName);
        Assert.AreEqual("CodeSnifferDog.Tests", result.Value.Projects[1].ProjectName);
    }

    [TestMethod]
    public async Task RunAsync_ResetsScanAgentConversation_AfterRepeatedMissingSubmissions()
    {
        int emptyAttempts = 0;

        ScanWorkflow workflow = CreateWorkflow(
            invocation =>
            {
                if (emptyAttempts < 3)
                {
                    emptyAttempts++;
                    return CreateAssistantResponse("No projects submitted yet.");
                }

                return CreateFunctionCallResponse(
                    "scan-add-primary",
                    "AddScanProject",
                    new Dictionary<string, object?>
                    {
                        ["ProjectName"] = "CodeSnifferDog",
                        ["ProjectPath"] = "CodeSnifferDog/CodeSnifferDog.csproj",
                        ["ProjectType"] = ".csproj",
                        ["Reason"] = "Primary application project.",
                    });
            },
            invocation => CreateFunctionCallResponse(
                "verdict-approve",
                "SubmitReviewVerdict",
                new Dictionary<string, object?>
                {
                    ["Approved"] = true,
                    ["Message"] = "The scan result is acceptable.",
                }),
            new ScanWorkflowOptions
            {
                MaxMissingSubmissionAttempts = 3,
                MaxScanAgentResets = 1,
            });

        var result = await workflow.RunAsync(@"Z:\GitHub\CodeSnifferDog", TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(1, result.Value.ScanAgentResetCount);
        Assert.AreEqual(4, result.Value.ScanAttempts);
        Assert.AreEqual(1, result.Value.VerifierAttempts);
    }

    [TestMethod]
    public async Task RunAsync_RecreatesScanAgentInstance_WhenResetOccurs()
    {
        int emptyAttempts = 0;
        int createdScanAgents = 0;
        ScriptedChatClient scanChatClient = new(invocation =>
        {
            if (emptyAttempts < 3)
            {
                emptyAttempts++;
                return CreateAssistantResponse("No projects submitted yet.");
            }

            return CreateFunctionCallResponse(
                "scan-add-primary",
                "AddScanProject",
                new Dictionary<string, object?>
                {
                    ["ProjectName"] = "CodeSnifferDog",
                    ["ProjectPath"] = "CodeSnifferDog/CodeSnifferDog.csproj",
                    ["ProjectType"] = ".csproj",
                    ["Reason"] = "Primary application project.",
                });
        });
        ScriptedChatClient verifierChatClient = new(invocation => CreateFunctionCallResponse(
            "verdict-approve",
            "SubmitReviewVerdict",
            new Dictionary<string, object?>
            {
                ["Approved"] = true,
                ["Message"] = "The scan result is acceptable.",
            }));
        InMemoryScanProjectStore scanProjectStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();
        ScanWorkflow workflow = new(
            repositoryRootPath =>
            {
                createdScanAgents++;
                return CreateScanAgent(repositoryRootPath, scanChatClient, scanProjectStore, verdictBuffer);
            },
            repositoryRootPath => CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProjectStore, verdictBuffer),
            scanProjectStore,
            verdictBuffer,
            promptAssetReader,
            new ScanWorkflowOptions
            {
                MaxMissingSubmissionAttempts = 3,
                MaxScanAgentResets = 1,
            });

        Result<ScanWorkflowResult> result = await workflow.RunAsync(@"Z:\GitHub\CodeSnifferDog", TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, createdScanAgents);
    }

    [TestMethod]
    public async Task RunAsync_ContinuesAfterVerifierRejectionLimit()
    {
        ScanWorkflow workflow = CreateWorkflow(
            invocation =>
            {
                if (HasFunctionResult(invocation.Messages, "scan-add-primary"))
                    return CreateAssistantResponse("Primary project recorded.");

                return CreateFunctionCallResponse(
                    "scan-add-primary",
                    "AddScanProject",
                    new Dictionary<string, object?>
                    {
                        ["ProjectName"] = "CodeSnifferDog",
                        ["ProjectPath"] = "CodeSnifferDog/CodeSnifferDog.csproj",
                        ["ProjectType"] = ".csproj",
                        ["Reason"] = "Primary application project.",
                    });
            },
            invocation => CreateFunctionCallResponse(
                $"verdict-reject-{invocation.CallIndex}",
                "SubmitReviewVerdict",
                new Dictionary<string, object?>
                {
                    ["Approved"] = false,
                    ["Message"] = "Add the missing test project before continuing.",
                }),
            new ScanWorkflowOptions
            {
                MaxVerifierRejectionAttempts = 3,
            });

        var result = await workflow.RunAsync(@"Z:\GitHub\CodeSnifferDog", TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(3, result.Value.ScanAttempts);
        Assert.AreEqual(3, result.Value.VerifierAttempts);
        Assert.IsFalse(result.Value.ScanVerifierApproved);
        Assert.IsTrue(result.Value.ContinuedAfterVerifierRejectionLimit);
        Assert.IsTrue(result.Value.ShouldEnterProjectPlanning);
        Assert.IsFalse(result.Value.Verdict.Approved);
        Assert.AreEqual("Add the missing test project before continuing.", result.Value.Verdict.Message);
        Assert.HasCount(1, result.Value.Projects);
    }

    [TestMethod]
    public async Task RunAsync_Fails_WhenScanAgentResetsAreExhausted()
    {
        ScanWorkflow workflow = CreateWorkflow(
            _ => CreateAssistantResponse("Still no scan projects."),
            _ => CreateAssistantResponse("Verifier should not run."),
            new ScanWorkflowOptions
            {
                MaxMissingSubmissionAttempts = 3,
                MaxScanAgentResets = 1,
            });

        var result = await workflow.RunAsync(@"Z:\GitHub\CodeSnifferDog", TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error =>
            error.Message.Contains("allowed reset limit", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_SendsConfiguredScanAndVerifierPrefixes()
    {
        bool scanPrefixObserved = false;
        bool verifierPrefixObserved = false;

        ScanWorkflow workflow = CreateWorkflow(
            invocation =>
            {
                scanPrefixObserved = invocation.Messages.Any(message =>
                    message.Role == ChatRole.User &&
                    message.Text?.StartsWith(CreateMessageTemplates().ScanInputPrefix, StringComparison.Ordinal) == true);

                return HandleScanInvocation(invocation);
            },
            invocation =>
            {
                verifierPrefixObserved = invocation.Messages.Any(message =>
                    message.Role == ChatRole.User &&
                    message.Text?.StartsWith(CreateMessageTemplates().VerifierInputPrefix, StringComparison.Ordinal) == true);

                return HandleVerifierInvocation(invocation);
            });

        var result = await workflow.RunAsync(@"Z:\GitHub\CodeSnifferDog", TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsTrue(scanPrefixObserved);
        Assert.IsTrue(verifierPrefixObserved);
    }

    [TestMethod]
    public async Task RunAsync_PreservesCompactionArtifacts_AndCompletesWorkflow()
    {
        List<ChatInvocation> scanInvocations = [];
        int scanFailures = 0;
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextAgentCompactionOptions compactionOptions = CreateCompactionOptions(
            ScanPromptAssetPaths.ScanSummaryPrompt,
            summarizer);
        ScriptedChatClient scanChatClient = new(invocation =>
        {
            scanInvocations.Add(invocation);

            if (scanFailures == 0)
            {
                scanFailures++;
                throw new OperationalContextModelInvocationException(
                    OperationalContextModelInvocationFailureKind.ContextWindowExceeded,
                    "context too large");
            }

            return HandleScanInvocation(invocation);
        });
        ScriptedChatClient verifierChatClient = new(HandleVerifierInvocation);
        InMemoryScanProjectStore scanProjectStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();
        ScanWorkflow workflow = new(
            repositoryRootPath => new ScanAgentFactory(compactionOptions).Create(
                scanChatClient,
                """
                You are the Scan Agent for CodeSnifferDog.

                Use the system-controlled user input as the source of truth for the repository root path.
                """,
                repositoryRootPath,
                scanProjectStore,
                verdictBuffer),
            repositoryRootPath => CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProjectStore, verdictBuffer),
            scanProjectStore,
            verdictBuffer,
            promptAssetReader);

        var result = await workflow.RunAsync(@"Z:\GitHub\CodeSnifferDog", TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsGreaterThan(0, summarizer.CallCount);
        Assert.IsGreaterThanOrEqualTo(2, scanInvocations.Count);
        Assert.IsTrue(summarizer.LastSummaryPrompt?.Contains("Summarize the current Scan-stage work", StringComparison.Ordinal) ?? false);

        ChatInvocation compactedInvocation = scanInvocations.First(invocation =>
            invocation.Messages.Any(IsSummaryArtifactMessage));

        Assert.AreEqual(1, compactedInvocation.Messages.Count(message => IsSummaryArtifactMessage(message)));
        Assert.IsTrue(compactedInvocation.Messages.Any(message =>
            message.Text?.Contains("Operational summary checkpoint", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task RunAsync_Fails_WhenVerifierDoesNotSubmitVerdict()
    {
        ScanWorkflow workflow = CreateWorkflow(
            invocation =>
            {
                if (HasFunctionResult(invocation.Messages, "scan-add-primary"))
                    return CreateAssistantResponse("Primary project recorded.");

                return CreateFunctionCallResponse(
                    "scan-add-primary",
                    "AddScanProject",
                    new Dictionary<string, object?>
                    {
                        ["ProjectName"] = "CodeSnifferDog",
                        ["ProjectPath"] = "CodeSnifferDog/CodeSnifferDog.csproj",
                        ["ProjectType"] = ".csproj",
                        ["Reason"] = "Primary application project.",
                    });
            },
            _ => CreateAssistantResponse("No verdict submitted."));

        var result = await workflow.RunAsync(@"Z:\GitHub\CodeSnifferDog", TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error =>
            error.Message.Contains("without submitting a verdict", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AddScanProjectAsync_RejectsBlankFields()
    {
        ScanToolSet toolSet = CreateToolSet();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => toolSet.AddScanProjectAsync(
            new AddScanProjectArgs
            {
                ProjectName = " ",
                ProjectPath = "CodeSnifferDog/CodeSnifferDog.csproj",
                ProjectType = ".csproj",
                Reason = "Primary application project.",
            },
            TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task AddScanProjectsAsync_RejectsEmptyList()
    {
        ScanToolSet toolSet = CreateToolSet();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => toolSet.AddScanProjectsAsync(
            new AddScanProjectsArgs
            {
                Projects = [],
            },
            TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task SubmitReviewVerdictAsync_RejectsBlankMessage()
    {
        ScanToolSet toolSet = CreateToolSet();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => toolSet.SubmitReviewVerdictAsync(
            new SubmitReviewVerdictArgs
            {
                Approved = false,
                Message = " ",
            },
            TestContext.CancellationToken).AsTask());
    }

    private static ScanWorkflow CreateWorkflow(
        Func<ChatInvocation, ChatResponse> scanResponseFactory,
        Func<ChatInvocation, ChatResponse> verifierResponseFactory,
        ScanWorkflowOptions? options = null)
    {
        ScriptedChatClient scanChatClient = new(scanResponseFactory);
        ScriptedChatClient verifierChatClient = new(verifierResponseFactory);
        InMemoryScanProjectStore scanProjectStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();

        return new ScanWorkflow(
            repositoryRootPath => CreateScanAgent(repositoryRootPath, scanChatClient, scanProjectStore, verdictBuffer),
            repositoryRootPath => CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProjectStore, verdictBuffer),
            scanProjectStore,
            verdictBuffer,
            promptAssetReader,
            options);
    }

    private static ScanToolSet CreateToolSet()
        =>
        new(new InMemoryScanProjectStore(), new ReviewVerdictBuffer());

    private static ScanWorkflowMessageTemplates CreateMessageTemplates()
        =>
        new(new PromptAssetReader());

    private static AIAgent CreateScanAgent(
        string repositoryRootPath,
        IChatClient chatClient,
        IScanProjectStore scanProjectStore,
        ReviewVerdictBuffer verdictBuffer) =>
        new ScanAgentFactory(CreateCompactionOptions(ScanPromptAssetPaths.ScanSummaryPrompt))
            .Create(chatClient, repositoryRootPath, scanProjectStore, verdictBuffer);

    private static AIAgent CreateVerifierAgent(
        string repositoryRootPath,
        IChatClient chatClient,
        IScanProjectStore scanProjectStore,
        ReviewVerdictBuffer verdictBuffer) =>
        new ScanVerifierAgentFactory(CreateCompactionOptions(ScanPromptAssetPaths.ScanSummaryPrompt))
            .Create(chatClient, repositoryRootPath, scanProjectStore, verdictBuffer);

    private static OperationalContextAgentCompactionOptions CreateCompactionOptions(
        string summaryPromptAssetPath,
        IOperationalContextCompactionSummarizer? summarizer = null) =>
        new OperationalContextAgentCompactionOptionsFactory(
            new PromptAssetReader(),
            summarizer ?? new RecordingSummarizer("<summary>Current objective\nCompleted work\nNext steps</summary>"),
            new FixedUsageProvider(usedTokens: 100))
            .CreateFromPromptAsset(
                summaryPromptAssetPath,
                new OperationalContextCompactionOptions
                {
                    ContextTokenThreshold = 10,
                });

    private static ChatResponse HandleScanInvocation(ChatInvocation invocation)
    {
        if (HasFunctionResult(invocation.Messages, "scan-add-test"))
            return CreateAssistantResponse("Correction scan recorded.");

        if (HasCorrectionInstruction(invocation.Messages))
            return CreateFunctionCallResponse(
                "scan-add-test",
                "AddScanProject",
                new Dictionary<string, object?>
                {
                    ["ProjectName"] = "CodeSnifferDog.Tests",
                    ["ProjectPath"] = "CodeSnifferDog.Tests/CodeSnifferDog.Tests.csproj",
                    ["ProjectType"] = ".csproj",
                    ["Reason"] = "Test project for the solution.",
                });

        if (HasFunctionResult(invocation.Messages, "scan-add-primary"))
            return CreateAssistantResponse("Initial scan recorded.");

        return CreateFunctionCallResponse(
            "scan-add-primary",
            "AddScanProject",
            new Dictionary<string, object?>
            {
                ["ProjectName"] = "CodeSnifferDog",
                ["ProjectPath"] = "CodeSnifferDog/CodeSnifferDog.csproj",
                ["ProjectType"] = ".csproj",
                ["Reason"] = "Primary application project.",
            });
    }

    private static ChatResponse HandleVerifierInvocation(ChatInvocation invocation)
    {
        if (HasFunctionResult(invocation.Messages, "verdict-approve"))
            return CreateAssistantResponse("Scan approved.");

        if (HasFunctionResult(invocation.Messages, "verdict-reject"))
            return CreateAssistantResponse("Scan requires one correction.");

        bool hasTestProject = invocation.Messages.Any(message =>
            message.Role == ChatRole.User &&
            message.Text?.Contains("CodeSnifferDog.Tests", StringComparison.Ordinal) == true);

        return CreateFunctionCallResponse(
            hasTestProject ? "verdict-approve" : "verdict-reject",
            "SubmitReviewVerdict",
            new Dictionary<string, object?>
            {
                ["Approved"] = hasTestProject,
                ["Message"] = hasTestProject
                    ? "The scan result covers the expected project-level structure."
                    : "Add the test project that is still missing from the scan result.",
            });
    }

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
            message.Text?.Contains("Add the test project", StringComparison.Ordinal) == true);

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

    private sealed class FixedUsageProvider(long usedTokens) : IOperationalContextCompactionUsageProvider
    {
        public ValueTask<OperationalContextCompactionUsage?> GetUsageAsync(
            IReadOnlyList<ChatMessage> messages,
            CancellationToken cancellationToken) => ValueTask.FromResult<OperationalContextCompactionUsage?>(new OperationalContextCompactionUsage
            {
                UsedTokens = usedTokens,
            });
    }

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
