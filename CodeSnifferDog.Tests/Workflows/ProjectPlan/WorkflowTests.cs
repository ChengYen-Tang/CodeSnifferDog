using CodeSnifferDog.Agents.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Workflows.ProjectPlan;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Failures;

namespace CodeSnifferDog.Tests.Workflows.ProjectPlan;

[TestClass]
[DoNotParallelize]
public sealed class WorkflowTests
{
    public required TestContext TestContext { get; init; }
    private static readonly string TestRepositoryRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [TestMethod]
    public async Task RunAsync_CompletesProjectPlanWorkflow_ThroughRealToolCalls()
    {
        Workflow workflow = CreateWorkflow(
            HandlePlanInvocation,
            HandleVerifierInvocation);

        var result = await workflow.RunAsync(TestRepositoryRootPath, CreateScanProject(), TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, result.Value.PlanAttempts);
        Assert.AreEqual(2, result.Value.VerifierAttempts);
        Assert.AreEqual(0, result.Value.ProjectPlanAgentResetCount);
        Assert.IsFalse(result.Value.ContinuedAfterVerifierRejectionLimit);
        Assert.IsTrue(result.Value.Verdict.Approved);
        Assert.HasCount(2, result.Value.TaskItems);
    }

    [TestMethod]
    public async Task RunAsync_ResetsProjectPlanAgentConversation_AfterRepeatedMissingSubmissions()
    {
        int emptyAttempts = 0;

        Workflow workflow = CreateWorkflow(
            invocation =>
            {
                if (emptyAttempts < 3)
                {
                    emptyAttempts++;
                    return CreateAssistantResponse("No task items submitted yet.");
                }

                return CreateFunctionCallResponse(
                    "plan-add-single",
                    "AddProjectPlanTaskItem",
                    new Dictionary<string, object?>
                    {
                        ["Files"] = new[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["FilePath"] = "CodeSnifferDog/Program.cs",
                                ["TotalLines"] = 120,
                            },
                        },
                    });
            },
            _ => CreateFunctionCallResponse(
                "verdict-approve",
                "SubmitReviewVerdict",
                new Dictionary<string, object?>
                {
                    ["Approved"] = true,
                    ["Message"] = "The project plan is acceptable.",
                }),
            new WorkflowOptions
            {
                MaxMissingSubmissionAttempts = 3,
                MaxProjectPlanAgentResets = 1,
            });

        var result = await workflow.RunAsync(TestRepositoryRootPath, CreateScanProject(), TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(1, result.Value.ProjectPlanAgentResetCount);
        Assert.AreEqual(4, result.Value.PlanAttempts);
        Assert.AreEqual(1, result.Value.VerifierAttempts);
    }

    [TestMethod]
    public async Task RunAsync_RecreatesProjectPlanAgentInstance_WhenResetOccurs()
    {
        int emptyAttempts = 0;
        int createdPlanAgents = 0;
        ScriptedChatClient planChatClient = new(invocation =>
        {
            if (emptyAttempts < 3)
            {
                emptyAttempts++;
                return CreateAssistantResponse("No task items submitted yet.");
            }

            return CreateFunctionCallResponse(
                "plan-add-single",
                "AddProjectPlanTaskItem",
                new Dictionary<string, object?>
                {
                    ["Files"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["FilePath"] = "CodeSnifferDog/Program.cs",
                            ["TotalLines"] = 120,
                        },
                    },
                });
        });
        ScriptedChatClient verifierChatClient = new(_ => CreateFunctionCallResponse(
            "verdict-approve",
            "SubmitReviewVerdict",
            new Dictionary<string, object?>
            {
                ["Approved"] = true,
                ["Message"] = "The project plan is acceptable.",
            }));
        InMemoryTaskItemStore taskItemStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();
        Workflow workflow = new(
            (repositoryRootPath, _) =>
            {
                createdPlanAgents++;
                return CreatePlanAgent(repositoryRootPath, planChatClient, taskItemStore, verdictBuffer);
            },
            (repositoryRootPath, scanProject, _) =>
                CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProject, taskItemStore, verdictBuffer),
            taskItemStore,
            verdictBuffer,
            promptAssetReader,
            new WorkflowOptions
            {
                MaxMissingSubmissionAttempts = 3,
                MaxProjectPlanAgentResets = 1,
            });

        Result<WorkflowResult> result =
            await workflow.RunAsync(TestRepositoryRootPath, CreateScanProject(), TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(2, createdPlanAgents);
    }

    [TestMethod]
    public async Task RunAsync_RetriesTimedOutAgentRuns_AndEventuallySucceeds()
    {
        int timedOutAttempts = 0;
        AsyncScriptedChatClient planChatClient = new(async (_, cancellationToken) =>
        {
            if (timedOutAttempts < 4)
            {
                timedOutAttempts++;
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return CreateFunctionCallResponse(
                "plan-add-single",
                "AddProjectPlanTaskItem",
                new Dictionary<string, object?>
                {
                    ["Files"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["FilePath"] = "CodeSnifferDog/Program.cs",
                            ["TotalLines"] = 120,
                        },
                    },
                });
        });
        ScriptedChatClient verifierChatClient = new(_ => CreateFunctionCallResponse(
            "verdict-approve",
            "SubmitReviewVerdict",
            new Dictionary<string, object?>
            {
                ["Approved"] = true,
                ["Message"] = "The project plan is acceptable.",
            }));
        InMemoryTaskItemStore taskItemStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();
        Workflow workflow = new(
            (repositoryRootPath, _) => CreatePlanAgent(repositoryRootPath, planChatClient, taskItemStore, verdictBuffer),
            (repositoryRootPath, scanProject, _) =>
                CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProject, taskItemStore, verdictBuffer),
            taskItemStore,
            verdictBuffer,
            promptAssetReader,
            new WorkflowOptions
            {
                AgentRunTimeout = TimeSpan.FromMilliseconds(250),
                MaxConsecutiveRunFailures = 5,
            });

        Result<WorkflowResult> result =
            await workflow.RunAsync(TestRepositoryRootPath, CreateScanProject(), TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(4, timedOutAttempts);
        Assert.AreEqual(1, result.Value.PlanAttempts);
        Assert.AreEqual(1, result.Value.VerifierAttempts);
        Assert.IsNotEmpty(result.Value.TaskItems);
    }

    [TestMethod]
    public async Task RunAsync_DegradesAgent_AfterFiveConsecutiveTimedOutRuns()
    {
        AsyncScriptedChatClient planChatClient = new(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CreateAssistantResponse("This response should never be returned.");
        });
        ScriptedChatClient verifierChatClient = new(_ => CreateAssistantResponse("Verifier should not run."));
        InMemoryTaskItemStore taskItemStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();
        Workflow workflow = new(
            (repositoryRootPath, _) => CreatePlanAgent(repositoryRootPath, planChatClient, taskItemStore, verdictBuffer),
            (repositoryRootPath, scanProject, _) =>
                CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProject, taskItemStore, verdictBuffer),
            taskItemStore,
            verdictBuffer,
            promptAssetReader,
            new WorkflowOptions
            {
                AgentRunTimeout = TimeSpan.FromMilliseconds(50),
                MaxConsecutiveRunFailures = 5,
            });

        Result<WorkflowResult> result =
            await workflow.RunAsync(TestRepositoryRootPath, CreateScanProject(), TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error =>
            error.Message.Contains("failed after 5 consecutive attempts", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_ContinuesAfterVerifierRejectionLimit()
    {
        Workflow workflow = CreateWorkflow(
            invocation =>
            {
                if (HasFunctionResult(invocation.Messages, "plan-add-infrastructure"))
                    return CreateAssistantResponse("Correction task item recorded.");

                if (HasCorrectionInstruction(invocation.Messages))
                {
                    return CreateFunctionCallResponse(
                        "plan-add-infrastructure",
                        "AddProjectPlanTaskItem",
                        new Dictionary<string, object?>
                        {
                            ["Files"] = new[]
                            {
                                new Dictionary<string, object?>
                                {
                                    ["FilePath"] = "CodeSnifferDog/Modules/Tools/Common/CommonToolSet.cs",
                                    ["TotalLines"] = 180,
                                },
                            },
                        });
                }

                if (HasFunctionResult(invocation.Messages, "plan-add-program"))
                    return CreateAssistantResponse("Initial task item recorded.");

                return CreateFunctionCallResponse(
                    "plan-add-program",
                    "AddProjectPlanTaskItem",
                    new Dictionary<string, object?>
                    {
                        ["Files"] = new[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["FilePath"] = "CodeSnifferDog/Program.cs",
                                ["TotalLines"] = 120,
                            },
                        },
                    });
            },
            _ => CreateFunctionCallResponse(
                "verdict-reject",
                "SubmitReviewVerdict",
                new Dictionary<string, object?>
                {
                    ["Approved"] = false,
                    ["Message"] = "Add the missing infrastructure task item before continuing.",
                }),
            new WorkflowOptions
            {
                MaxVerifierRejectionAttempts = 3,
            });

        var result = await workflow.RunAsync(TestRepositoryRootPath, CreateScanProject(), TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.AreEqual(3, result.Value.PlanAttempts);
        Assert.AreEqual(3, result.Value.VerifierAttempts);
        Assert.IsTrue(result.Value.ContinuedAfterVerifierRejectionLimit);
        Assert.IsFalse(result.Value.Verdict.Approved);
    }

    [TestMethod]
    public async Task RunAsync_Fails_WhenProjectPlanAgentResetsAreExhausted()
    {
        Workflow workflow = CreateWorkflow(
            _ => CreateAssistantResponse("Still no task items."),
            _ => CreateAssistantResponse("Verifier should not run."),
            new WorkflowOptions
            {
                MaxMissingSubmissionAttempts = 3,
                MaxProjectPlanAgentResets = 1,
            });

        var result = await workflow.RunAsync(TestRepositoryRootPath, CreateScanProject(), TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error =>
            error.Message.Contains("allowed reset limit", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task RunAsync_SendsConfiguredPlanAndVerifierPrefixes()
    {
        bool planPrefixObserved = false;
        bool verifierPrefixObserved = false;

        Workflow workflow = CreateWorkflow(
            invocation =>
            {
                planPrefixObserved = invocation.Messages.Any(message =>
                    message.Role == ChatRole.User &&
                    message.Text?.StartsWith(CreateMessageTemplates().PlanInputPrefix, StringComparison.Ordinal) == true);

                return HandlePlanInvocation(invocation);
            },
            invocation =>
            {
                verifierPrefixObserved = invocation.Messages.Any(message =>
                    message.Role == ChatRole.User &&
                    message.Text?.StartsWith(CreateMessageTemplates().VerifierInputPrefix, StringComparison.Ordinal) == true);

                return HandleVerifierInvocation(invocation);
            });

        var result = await workflow.RunAsync(TestRepositoryRootPath, CreateScanProject(), TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsTrue(planPrefixObserved);
        Assert.IsTrue(verifierPrefixObserved);
    }

    [TestMethod]
    public async Task RunAsync_PassesCurrentScanProject_ToVerifierPromptContext()
    {
        StoredScanProject scanProject = new()
        {
            ScanProjectId = "scan-project-runtime",
            ProjectName = "RuntimeProject",
            ProjectPath = "src/RuntimeProject/RuntimeProject.csproj",
            ProjectType = ".csproj",
            Reason = "RuntimeContext-selected project.",
        };
        string? verifierPrompt = null;
        ScriptedChatClient planChatClient = new(HandlePlanInvocation);
        ScriptedChatClient verifierChatClient = new(_ => CreateFunctionCallResponse(
            "verdict-approve",
            "SubmitReviewVerdict",
            new Dictionary<string, object?>
            {
                ["Approved"] = true,
                ["Message"] = "The project plan is acceptable.",
            }));
        InMemoryTaskItemStore taskItemStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();
        Workflow workflow = new(
            (repositoryRootPath, _) => CreatePlanAgent(repositoryRootPath, planChatClient, taskItemStore, verdictBuffer),
            (repositoryRootPath, currentScanProject, _) =>
            {
                VerifierFactory factory =
                    new(CreateCompactionOptions(ProjectPlanAgentPromptAssets.ProjectPlanSummaryPrompt));
                AgentCreationResult createdAgent = factory.Create(
                    verifierChatClient,
                    repositoryRootPath,
                    currentScanProject,
                    taskItemStore,
                    verdictBuffer);

                verifierPrompt = createdAgent.SystemPrompt;
                return createdAgent;
            },
            taskItemStore,
            verdictBuffer,
            promptAssetReader);

        Result<WorkflowResult> result =
            await workflow.RunAsync(TestRepositoryRootPath, scanProject, TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsNotNull(verifierPrompt);
        Assert.IsTrue(verifierPrompt.Contains("RuntimeProject", StringComparison.Ordinal));
        Assert.IsTrue(verifierPrompt.Contains("scan-project-runtime", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RunAsync_PublishesProjectPlanGroupDisplayName_WithProjectName()
    {
        RecordingGroupCreatedAgentEventBus eventBus = new();
        StoredScanProject scanProject = new()
        {
            ScanProjectId = "scan-project-runtime",
            ProjectName = "RuntimeProject",
            ProjectPath = "src/RuntimeProject/RuntimeProject.csproj",
            ProjectType = ".csproj",
            Reason = "RuntimeContext-selected project.",
        };
        Workflow workflow = CreateWorkflow(
            HandlePlanInvocation,
            HandleVerifierInvocation,
            agentEventBus: eventBus);

        Result<WorkflowResult> result =
            await workflow.RunAsync(TestRepositoryRootPath, scanProject, TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        CollectionAssert.AreEqual(
            new[] { "Project Plan: RuntimeProject" },
            eventBus.GroupCreatedDisplayNames.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_PreservesCompactionArtifacts_AndCompletesWorkflow()
    {
        List<ChatInvocation> planInvocations = [];
        int planFailures = 0;
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        AgentCompactionOptions compactionOptions = CreateCompactionOptions(
            ProjectPlanAgentPromptAssets.ProjectPlanSummaryPrompt,
            summarizer);
        ScriptedChatClient planChatClient = new(invocation =>
        {
            planInvocations.Add(invocation);

            if (planFailures == 0)
            {
                planFailures++;
                throw new ModelInvocationException(
                    ModelInvocationFailureKind.ContextWindowExceeded,
                    "context too large");
            }

            return HandlePlanInvocation(invocation);
        });
        ScriptedChatClient verifierChatClient = new(HandleVerifierInvocation);
        InMemoryTaskItemStore taskItemStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();
        Workflow workflow = new(
            (repositoryRootPath, _) => new AgentFactory(compactionOptions).Create(
                planChatClient,
                repositoryRootPath,
                taskItemStore,
                verdictBuffer),
            (repositoryRootPath, scanProject, _) =>
                CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProject, taskItemStore, verdictBuffer),
            taskItemStore,
            verdictBuffer,
            promptAssetReader);

        var result = await workflow.RunAsync(TestRepositoryRootPath, CreateScanProject(), TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        Assert.IsGreaterThan(0, summarizer.CallCount);
        Assert.IsGreaterThanOrEqualTo(2, planInvocations.Count);
        Assert.IsTrue(summarizer.LastSummaryPrompt?.Contains("Summarize the current Project Planning-stage work", StringComparison.Ordinal) ?? false);

        ChatInvocation compactedInvocation = planInvocations.First(invocation =>
            invocation.Messages.Any(IsSummaryArtifactMessage));

        Assert.AreEqual(1, compactedInvocation.Messages.Count(message => IsSummaryArtifactMessage(message)));
        Assert.IsTrue(compactedInvocation.Messages.Any(message =>
            message.Text?.Contains("Operational summary checkpoint", StringComparison.Ordinal) == true));
    }

    [TestMethod]
    public async Task RunAsync_Fails_WhenVerifierDoesNotSubmitVerdict()
    {
        Workflow workflow = CreateWorkflow(
            HandlePlanInvocation,
            _ => CreateAssistantResponse("No verdict submitted."));

        var result = await workflow.RunAsync(TestRepositoryRootPath, CreateScanProject(), TestContext.CancellationToken);

        Assert.IsTrue(result.IsFailed);
        Assert.IsTrue(result.Errors.Any(error =>
            error.Message.Contains("without submitting a verdict", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task AddProjectPlanTaskItemAsync_RejectsBlankFields()
    {
        ToolSet toolSet = CreateToolSet();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => toolSet.AddProjectPlanTaskItemAsync(
            new AddProjectPlanTaskItemArgs
            {
                Files =
                [
                    new PlanFile
                    {
                        FilePath = " ",
                        TotalLines = 10,
                    },
                ],
            },
            TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task AddProjectPlanTaskItemsAsync_RejectsEmptyList()
    {
        ToolSet toolSet = CreateToolSet();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => toolSet.AddProjectPlanTaskItemsAsync(
            new AddProjectPlanTaskItemsArgs
            {
                TaskItems = [],
            },
            TestContext.CancellationToken).AsTask());
    }

    [TestMethod]
    public async Task SubmitReviewVerdictAsync_RejectsBlankMessage()
    {
        ToolSet toolSet = CreateToolSet();

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => toolSet.SubmitReviewVerdictAsync(
            new SubmitReviewVerdictArgs
            {
                Approved = false,
                Message = " ",
            },
            TestContext.CancellationToken).AsTask());
    }

    private static Workflow CreateWorkflow(
        Func<ChatInvocation, ChatResponse> planResponseFactory,
        Func<ChatInvocation, ChatResponse> verifierResponseFactory,
        WorkflowOptions? options = null,
        IAgentEventBus? agentEventBus = null)
    {
        ScriptedChatClient planChatClient = new(planResponseFactory);
        ScriptedChatClient verifierChatClient = new(verifierResponseFactory);
        InMemoryTaskItemStore taskItemStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        PromptAssetReader promptAssetReader = new();

        return new Workflow(
            (repositoryRootPath, _) => CreatePlanAgent(repositoryRootPath, planChatClient, taskItemStore, verdictBuffer),
            (repositoryRootPath, scanProject, _) =>
                CreateVerifierAgent(repositoryRootPath, verifierChatClient, scanProject, taskItemStore, verdictBuffer),
            taskItemStore,
            verdictBuffer,
            promptAssetReader,
            options,
            agentEventBus);
    }

    private static ToolSet CreateToolSet()
        =>
        new(new InMemoryTaskItemStore(), new ReviewVerdictBuffer());

    private static MessageTemplates CreateMessageTemplates()
        =>
        new(new PromptAssetReader());

    private static StoredScanProject CreateScanProject()
        =>
        new()
        {
            ScanProjectId = "scan-project-1",
            ProjectName = "CodeSnifferDog",
            ProjectPath = "CodeSnifferDog/CodeSnifferDog.csproj",
            ProjectType = ".csproj",
            Reason = "Primary application project.",
        };

    private static AgentCreationResult CreatePlanAgent(
        string repositoryRootPath,
        IChatClient chatClient,
        ITaskItemStore taskItemStore,
        ReviewVerdictBuffer verdictBuffer) =>
        new AgentFactory(CreateCompactionOptions(ProjectPlanAgentPromptAssets.ProjectPlanSummaryPrompt))
            .Create(chatClient, repositoryRootPath, taskItemStore, verdictBuffer);

    private static AgentCreationResult CreateVerifierAgent(
        string repositoryRootPath,
        IChatClient chatClient,
        StoredScanProject scanProject,
        ITaskItemStore taskItemStore,
        ReviewVerdictBuffer verdictBuffer) =>
        new VerifierFactory(CreateCompactionOptions(ProjectPlanAgentPromptAssets.ProjectPlanSummaryPrompt))
            .Create(chatClient, repositoryRootPath, scanProject, taskItemStore, verdictBuffer);

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

    private static ChatResponse HandlePlanInvocation(ChatInvocation invocation)
    {
        if (HasFunctionResult(invocation.Messages, "plan-add-tools"))
            return CreateAssistantResponse("Correction plan recorded.");

        if (HasCorrectionInstruction(invocation.Messages))
        {
            return CreateFunctionCallResponse(
                "plan-add-tools",
                "AddProjectPlanTaskItem",
                new Dictionary<string, object?>
                {
                    ["Files"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["FilePath"] = "CodeSnifferDog/Modules/Tools/Common/CommonToolSet.cs",
                            ["TotalLines"] = 180,
                        },
                    },
                });
        }

        if (HasFunctionResult(invocation.Messages, "plan-add-core"))
            return CreateAssistantResponse("Initial plan recorded.");

        return CreateFunctionCallResponse(
            "plan-add-core",
            "AddProjectPlanTaskItems",
            new Dictionary<string, object?>
            {
                ["TaskItems"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["Files"] = new[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["FilePath"] = "CodeSnifferDog/Program.cs",
                                ["TotalLines"] = 120,
                            },
                            new Dictionary<string, object?>
                            {
                                ["FilePath"] = "CodeSnifferDog/CodeSnifferDog.csproj",
                                ["TotalLines"] = 35,
                            },
                        },
                    },
                },
            });
    }

    private static ChatResponse HandleVerifierInvocation(ChatInvocation invocation)
    {
        if (HasFunctionResult(invocation.Messages, "verdict-approve"))
            return CreateAssistantResponse("Project plan approved.");

        if (HasFunctionResult(invocation.Messages, "verdict-reject"))
            return CreateAssistantResponse("Project plan requires one correction.");

        bool hasToolsTaskItem = invocation.Messages.Any(message =>
            message.Role == ChatRole.User &&
            message.Text?.Contains("CommonToolSet.cs", StringComparison.Ordinal) == true);

        return CreateFunctionCallResponse(
            hasToolsTaskItem ? "verdict-approve" : "verdict-reject",
            "SubmitReviewVerdict",
            new Dictionary<string, object?>
            {
                ["Approved"] = hasToolsTaskItem,
                ["Message"] = hasToolsTaskItem
                    ? "The task items cover the expected project files with acceptable grouping."
                    : "Add the missing infrastructure task item that covers CommonToolSet.cs before continuing.",
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
            message.Text?.Contains("missing infrastructure task item", StringComparison.Ordinal) == true);

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

    private sealed class RecordingGroupCreatedAgentEventBus : IAgentEventBus
    {
        public List<string> GroupCreatedDisplayNames { get; } = [];

        public IAgentEventScope CreateScope(string groupKey, string agentKey) =>
            new NoOpAgentEventScope(groupKey, agentKey);

        public ValueTask PublishGroupCreatedAsync(
            string groupKey,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            GroupCreatedDisplayNames.Add(displayName);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoOpAgentEventScope(string groupKey, string agentKey) : IAgentEventScope
    {
        public string GroupKey { get; } = groupKey;

        public string AgentKey { get; } = agentKey;

        public ValueTask PublishCreatedAsync(string displayName, string systemPrompt, string initialStatus, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishStatusChangedAsync(string status, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishUserMessageAsync(string message, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishAssistantMessageAsync(string message, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishToolCallStartedAsync(string toolCallId, string toolName, string? arguments, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishToolCallCompletedAsync(string toolCallId, string? result, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishCompactionAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask PublishTranscriptClearedAsync(DateTimeOffset clearAfterUtc, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

}
