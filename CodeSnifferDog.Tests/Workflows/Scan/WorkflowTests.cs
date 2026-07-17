using CodeSnifferDog.Agents.Scan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.Scan;
using CodeSnifferDog.Workflows.Common;
using CodeSnifferDog.Workflows.Scan;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Failures;

namespace CodeSnifferDog.Tests.Workflows.Scan;

[TestClass]
[DoNotParallelize]
public sealed class WorkflowTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunAsync_CompletesScanWorkflow_ThroughRealToolCalls()
    {
        Workflow workflow = CreateWorkflow(
            HandleScanInvocation,
            HandleVerifierInvocation);

        var result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, result.Value.ScanAttempts);
        Assert.AreEqual(2, result.Value.VerifierAttempts);
        Assert.AreEqual(0, result.Value.ScanAgentResetCount);
        Assert.IsTrue(result.Value.Verdict.Approved);
        Assert.HasCount(2, result.Value.Projects);
        Assert.AreEqual("CodeSnifferDog", result.Value.Projects[0].ProjectName);
        Assert.AreEqual("CodeSnifferDog.Tests", result.Value.Projects[1].ProjectName);
    }

    [TestMethod]
    public async Task RunAsync_ResetsScanAgentConversation_AfterRepeatedMissingSubmissions()
    {
        int emptyAttempts = 0;

        Workflow workflow = CreateWorkflow(
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

        var result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);

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
        Workflow workflow = new(
            (repositoryRootPath, _) =>
            {
                createdScanAgents++;
                return CreateScanAgent(repositoryRootPath, scanChatClient, scanProjectStore, verdictBuffer);
            },
            (repositoryRootPath, _) => CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProjectStore, verdictBuffer),
            scanProjectStore,
            verdictBuffer,
            promptAssetReader,
            new ScanWorkflowOptions
            {
                MaxMissingSubmissionAttempts = 3,
                MaxScanAgentResets = 1,
            });

        Result<ScanWorkflowResult> result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, createdScanAgents);
    }

    [TestMethod]
    public async Task RunAsync_RetriesTimedOutAgentRuns_AndEventuallySucceeds()
    {
        int timedOutAttempts = 0;
        AsyncScriptedChatClient scanChatClient = new(async (invocation, cancellationToken) =>
        {
            if (timedOutAttempts < 4)
            {
                timedOutAttempts++;
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
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
        ScriptedChatClient verifierChatClient = new(_ => CreateFunctionCallResponse(
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
        Workflow workflow = new(
            (repositoryRootPath, _) => CreateScanAgent(repositoryRootPath, scanChatClient, scanProjectStore, verdictBuffer),
            (repositoryRootPath, _) => CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProjectStore, verdictBuffer),
            scanProjectStore,
            verdictBuffer,
            promptAssetReader,
            new ScanWorkflowOptions
            {
                AgentRunTimeout = TimeSpan.FromMilliseconds(250),
                MaxConsecutiveRunFailures = 5,
            });

        Result<ScanWorkflowResult> result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(4, timedOutAttempts);
        Assert.AreEqual(1, result.Value.ScanAttempts);
        Assert.AreEqual(1, result.Value.VerifierAttempts);
        Assert.IsNotEmpty(result.Value.Projects);
    }

    [TestMethod]
    public async Task RunAsync_DegradesAgent_AfterFiveConsecutiveTimedOutRuns()
    {
        RecordingAgentEventBus eventBus = new();
        AsyncScriptedChatClient scanChatClient = new(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CreateAssistantResponse("This response should never be returned.");
        });
        ScriptedChatClient verifierChatClient = new(_ => CreateAssistantResponse("Verifier should not run."));
        InMemoryScanProjectStore scanProjectStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();
        Workflow workflow = new(
            (repositoryRootPath, _) => CreateScanAgent(repositoryRootPath, scanChatClient, scanProjectStore, verdictBuffer),
            (repositoryRootPath, _) => CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProjectStore, verdictBuffer),
            scanProjectStore,
            verdictBuffer,
            promptAssetReader,
            new ScanWorkflowOptions
            {
                AgentRunTimeout = TimeSpan.FromMilliseconds(50),
                MaxConsecutiveRunFailures = 5,
            },
            eventBus);

        Result<ScanWorkflowResult> result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error =>
            error.Message.Contains("failed after 5 consecutive attempts", StringComparison.Ordinal)));
        Assert.IsTrue(eventBus.Events.Any(record =>
            record.EventType == "status" &&
            record.AgentKey == AgentStatusCatalog.CreateScanAgentKey() &&
            record.Payload == AgentStatusCatalog.DegradedStatus));
    }

    [TestMethod]
    public async Task RunAsync_IgnoresLateWrites_FromTimedOutAttempt()
    {
        TaskCompletionSource staleWriteObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int timedOutAttempts = 0;
        InMemoryScanProjectStore scanProjectStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        AsyncScriptedChatClient scanChatClient = new(async (invocation, cancellationToken) =>
        {
            if (timedOutAttempts == 0)
            {
                timedOutAttempts++;
                Task backgroundWriteTask = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(150, CancellationToken.None);
                        await scanProjectStore.AddAsync(
                            new ScanProject
                            {
                                ProjectName = "StaleProject",
                                ProjectPath = "CodeSnifferDog/StaleProject.csproj",
                                ProjectType = ".csproj",
                                Reason = "Late write from timed out attempt.",
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
        ScriptedChatClient verifierChatClient = new(_ => CreateFunctionCallResponse(
            "verdict-approve",
            "SubmitReviewVerdict",
            new Dictionary<string, object?>
            {
                ["Approved"] = true,
                ["Message"] = "The scan result is acceptable.",
            }));
        PromptAssetReader promptAssetReader = new();
        Workflow workflow = new(
            (repositoryRootPath, _) => CreateScanAgent(repositoryRootPath, scanChatClient, scanProjectStore, verdictBuffer),
            (repositoryRootPath, _) => CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProjectStore, verdictBuffer),
            scanProjectStore,
            verdictBuffer,
            promptAssetReader,
            new ScanWorkflowOptions
            {
                AgentRunTimeout = TimeSpan.FromMilliseconds(50),
                MaxConsecutiveRunFailures = 5,
            });

        Result<ScanWorkflowResult> result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);
        await staleWriteObserved.Task.WaitAsync(TestContext.CancellationToken);
        IReadOnlyList<StoredScanProject> persistedProjects = await scanProjectStore.ListAsync(TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(1, timedOutAttempts);
        Assert.HasCount(1, persistedProjects);
        Assert.AreEqual("CodeSnifferDog", persistedProjects[0].ProjectName);
    }

    [TestMethod]
    public async Task RunAsync_ContinuesAfterVerifierRejectionLimit()
    {
        Workflow workflow = CreateWorkflow(
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

        var result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(3, result.Value.ScanAttempts);
        Assert.AreEqual(3, result.Value.VerifierAttempts);
        Assert.IsFalse(result.Value.Verdict.Approved);
        Assert.AreEqual("Add the missing test project before continuing.", result.Value.Verdict.Message);
        Assert.HasCount(1, result.Value.Projects);
    }

    [TestMethod]
    public async Task RunAsync_Fails_WhenScanAgentResetsAreExhausted()
    {
        Workflow workflow = CreateWorkflow(
            _ => CreateAssistantResponse("Still no scan projects."),
            _ => CreateAssistantResponse("Verifier should not run."),
            new ScanWorkflowOptions
            {
                MaxMissingSubmissionAttempts = 3,
                MaxScanAgentResets = 1,
            });

        var result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error =>
            error.Message.Contains("allowed reset limit", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_SendsConfiguredScanAndVerifierPrefixes()
    {
        bool scanPrefixObserved = false;
        bool verifierPrefixObserved = false;

        Workflow workflow = CreateWorkflow(
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

        var result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsTrue(scanPrefixObserved);
        Assert.IsTrue(verifierPrefixObserved);
    }

    [TestMethod]
    public async Task RunAsync_PublishesUserAndAssistantMessages_ViaAgentEventScope()
    {
        RecordingAgentEventBus eventBus = new();
        Workflow workflow = CreateWorkflow(
            HandleScanInvocation,
            HandleVerifierInvocation,
            agentEventBus: eventBus);

        Result<ScanWorkflowResult> result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsTrue(eventBus.Events.Any(record => record.EventType == "user" && record.AgentKey == AgentStatusCatalog.CreateScanAgentKey()));
        Assert.IsTrue(eventBus.Events.Any(record => record.EventType == "assistant" && record.AgentKey == AgentStatusCatalog.CreateScanAgentKey()));

        int firstUserIndex = eventBus.Events.FindIndex(record =>
            record.EventType == "user" && record.AgentKey == AgentStatusCatalog.CreateScanAgentKey());
        int firstAssistantIndex = eventBus.Events.FindIndex(record =>
            record.EventType == "assistant" && record.AgentKey == AgentStatusCatalog.CreateScanAgentKey());

        Assert.IsGreaterThanOrEqualTo(0, firstUserIndex);
        Assert.IsGreaterThanOrEqualTo(0, firstAssistantIndex);
        Assert.IsLessThan(firstAssistantIndex, firstUserIndex);
    }

    [TestMethod]
    public async Task RunAsync_PublishesToolCallLifecycle_ViaAgentEventScope()
    {
        RecordingAgentEventBus eventBus = new();
        Workflow workflow = CreateWorkflow(
            HandleScanInvocation,
            HandleVerifierInvocation,
            agentEventBus: eventBus);

        Result<ScanWorkflowResult> result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsTrue(eventBus.Events.Any(record =>
            record.EventType == "tool-start" &&
            record.AgentKey == AgentStatusCatalog.CreateScanAgentKey() &&
            record.ToolCallId == "scan-add-primary" &&
            record.Payload == "AddScanProject"));
        Assert.IsTrue(eventBus.Events.Any(record =>
            record.EventType == "tool-complete" &&
            record.AgentKey == AgentStatusCatalog.CreateScanAgentKey() &&
            record.ToolCallId == "scan-add-primary"));
    }

    [TestMethod]
    public async Task RunAsync_PreservesCompactionArtifacts_AndCompletesWorkflow()
    {
        List<ChatInvocation> scanInvocations = [];
        int scanFailures = 0;
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        AgentCompactionOptions compactionOptions = CreateCompactionOptions(
            ScanAgentPromptAssets.ScanSummaryPrompt,
            summarizer);
        ScriptedChatClient scanChatClient = new(invocation =>
        {
            scanInvocations.Add(invocation);

            if (scanFailures == 0)
            {
                scanFailures++;
                throw new ModelInvocationException(
                    ModelInvocationFailureKind.ContextWindowExceeded,
                    "context too large");
            }

            return HandleScanInvocation(invocation);
        });
        ScriptedChatClient verifierChatClient = new(HandleVerifierInvocation);
        InMemoryScanProjectStore scanProjectStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();
        Workflow workflow = new(
            (repositoryRootPath, _) => new ScanAgentFactory(compactionOptions).Create(
                scanChatClient,
                repositoryRootPath,
                scanProjectStore,
                verdictBuffer),
            (repositoryRootPath, _) => CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProjectStore, verdictBuffer),
            scanProjectStore,
            verdictBuffer,
            promptAssetReader);

        var result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);

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
        Workflow workflow = CreateWorkflow(
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

        var result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error =>
            error.Message.Contains("without submitting a verdict", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_RetriesVerifierWithUserReminder_WhenVerifierDoesNotSubmitVerdict()
    {
        int verifierInvocations = 0;
        bool reminderObserved = false;
        Workflow workflow = CreateWorkflow(
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
            invocation =>
            {
                verifierInvocations++;
                reminderObserved |= invocation.Messages.Any(message =>
                    message.Role == ChatRole.User &&
                    message.Text == WorkflowRetryMessages.MissingVerifierVerdictMessage);

                if (HasFunctionResult(invocation.Messages, "verdict-approve"))
                    return CreateAssistantResponse("Scan approved.");

                if (verifierInvocations == 1)
                    return CreateAssistantResponse("No verdict submitted.");

                return CreateFunctionCallResponse(
                    "verdict-approve",
                    "SubmitReviewVerdict",
                    new Dictionary<string, object?>
                    {
                        ["Approved"] = true,
                        ["Message"] = "The scan result is acceptable.",
                    });
            });

        Result<ScanWorkflowResult> result = await workflow.RunAsync(TestRepositoryPaths.RootPath, TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, result.Value.VerifierAttempts);
        Assert.AreEqual(3, verifierInvocations);
        Assert.IsTrue(reminderObserved);
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

    private static Workflow CreateWorkflow(
        Func<ChatInvocation, ChatResponse> scanResponseFactory,
        Func<ChatInvocation, ChatResponse> verifierResponseFactory,
        ScanWorkflowOptions? options = null,
        IAgentEventBus? agentEventBus = null)
    {
        ScriptedChatClient scanChatClient = new(scanResponseFactory);
        ScriptedChatClient verifierChatClient = new(verifierResponseFactory);
        InMemoryScanProjectStore scanProjectStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();

        return new Workflow(
            (repositoryRootPath, _) => CreateScanAgent(repositoryRootPath, scanChatClient, scanProjectStore, verdictBuffer),
            (repositoryRootPath, _) => CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProjectStore, verdictBuffer),
            scanProjectStore,
            verdictBuffer,
            promptAssetReader,
            options,
            agentEventBus);
    }

    private static ScanToolSet CreateToolSet()
        =>
        new(new InMemoryScanProjectStore(), new ReviewVerdictBuffer());

    private static MessageTemplates CreateMessageTemplates()
        =>
        new(new PromptAssetReader());

    private static AgentCreationResult CreateScanAgent(
        string repositoryRootPath,
        IChatClient chatClient,
        IScanProjectStore scanProjectStore,
        ReviewVerdictBuffer verdictBuffer) =>
        new ScanAgentFactory(CreateCompactionOptions(ScanAgentPromptAssets.ScanSummaryPrompt))
            .Create(chatClient, repositoryRootPath, scanProjectStore, verdictBuffer);

    private static AgentCreationResult CreateVerifierAgent(
        string repositoryRootPath,
        IChatClient chatClient,
        IScanProjectStore scanProjectStore,
        ReviewVerdictBuffer verdictBuffer) =>
        new ScanVerifierAgentFactory(CreateCompactionOptions(ScanAgentPromptAssets.ScanSummaryPrompt))
            .Create(chatClient, repositoryRootPath, scanProjectStore, verdictBuffer);

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

    private sealed class RecordingAgentEventBus : IAgentEventBus
    {
        public List<EventRecord> Events { get; } = [];

        public IAgentEventScope CreateScope(string groupKey, string agentKey) =>
            new RecordingAgentEventScope(groupKey, agentKey, Events);

        public ValueTask PublishGroupCreatedAsync(
            string groupKey,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            Events.Add(new EventRecord("group", groupKey, null, displayName, null));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingAgentEventScope(
        string groupKey,
        string agentKey,
        List<EventRecord> events) : IAgentEventScope
    {
        public string GroupKey { get; } = groupKey;

        public string AgentKey { get; } = agentKey;

        public ValueTask PublishCreatedAsync(
            string displayName,
            string systemPrompt,
            string initialStatus,
            CancellationToken cancellationToken = default)
        {
            events.Add(new EventRecord("created", GroupKey, AgentKey, displayName, null));
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishStatusChangedAsync(
            string status,
            CancellationToken cancellationToken = default)
        {
            events.Add(new EventRecord("status", GroupKey, AgentKey, status, null));
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishUserMessageAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            events.Add(new EventRecord("user", GroupKey, AgentKey, message, null));
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAssistantMessageAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            events.Add(new EventRecord("assistant", GroupKey, AgentKey, message, null));
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishToolCallStartedAsync(
            string toolCallId,
            string toolName,
            string? arguments,
            CancellationToken cancellationToken = default)
        {
            events.Add(new EventRecord("tool-start", GroupKey, AgentKey, toolName, toolCallId));
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishToolCallCompletedAsync(
            string toolCallId,
            string? result,
            CancellationToken cancellationToken = default)
        {
            events.Add(new EventRecord("tool-complete", GroupKey, AgentKey, result, toolCallId));
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishCompactionAsync(CancellationToken cancellationToken = default)
        {
            events.Add(new EventRecord("compaction", GroupKey, AgentKey, null, null));
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishTranscriptClearedAsync(
            DateTimeOffset clearAfterUtc,
            CancellationToken cancellationToken = default)
        {
            events.Add(new EventRecord("clear", GroupKey, AgentKey, clearAfterUtc.ToString("O"), null));
            return ValueTask.CompletedTask;
        }
    }

    private sealed record EventRecord(string EventType, string GroupKey, string? AgentKey, string? Payload, string? ToolCallId);
}
