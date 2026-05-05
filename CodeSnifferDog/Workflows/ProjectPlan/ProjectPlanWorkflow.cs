using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Review;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CodeSnifferDog.Workflows.ProjectPlan;

public sealed class ProjectPlanWorkflow(
    Func<string, AIAgent> projectPlanAgentFactory,
    Func<string, StoredScanProject, AIAgent> projectVerifierAgentFactory,
    IProjectPlanTaskItemStore taskItemStore,
    ReviewVerdictBuffer verdictBuffer,
    PromptAssetReader? promptAssetReader = null,
    ProjectPlanWorkflowOptions? options = null,
    IAgentStatusEventPublisher? agentStatusEventPublisher = null)
{
    private readonly Func<string, AIAgent> _projectPlanAgentFactory = projectPlanAgentFactory;
    private readonly Func<string, StoredScanProject, AIAgent> _projectVerifierAgentFactory = projectVerifierAgentFactory;
    private readonly IProjectPlanTaskItemStore _taskItemStore = taskItemStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader ?? new();
    private readonly ProjectPlanWorkflowOptions _options = options ?? new();
    private readonly IAgentStatusEventPublisher _agentStatusEventPublisher = agentStatusEventPublisher ?? NoOpAgentStatusEventPublisher.Instance;

    public async Task<Result<ProjectPlanWorkflowResult>> RunAsync(
        string repositoryRootPath,
        StoredScanProject scanProject,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<ProjectPlanWorkflowResult>("Repository root path is required.");

        ArgumentNullException.ThrowIfNull(scanProject);

        repositoryRootPath = repositoryRootPath.Trim();
        await _taskItemStore.ClearAsync(cancellationToken).ConfigureAwait(false);

        ProjectPlanWorkflowMessageTemplates messageTemplates = new(_promptAssetReader);
        string groupKey = AgentStatusCatalog.CreateProjectPlanGroupKey(scanProject);
        string plannerAgentKey = AgentStatusCatalog.CreateProjectPlannerAgentKey(scanProject);
        string verifierAgentKey = AgentStatusCatalog.CreateProjectVerifierAgentKey(scanProject);

        await _agentStatusEventPublisher.PublishAsync(new AgentGroupCreatedEvent
        {
            GroupKey = groupKey,
            DisplayName = AgentStatusCatalog.CreateProjectPlanGroupDisplayName(scanProject),
            OccurredAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken).ConfigureAwait(false);

        AIAgent projectPlanAgent = _projectPlanAgentFactory(repositoryRootPath);
        await _agentStatusEventPublisher.PublishAsync(new AgentCreatedEvent
        {
            AgentKey = plannerAgentKey,
            DisplayName = AgentStatusCatalog.CreateProjectPlannerAgentDisplayName(),
            InitialStatus = AgentStatusCatalog.WaitingStatus,
            GroupKey = groupKey,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken).ConfigureAwait(false);

        AIAgent projectVerifierAgent = _projectVerifierAgentFactory(repositoryRootPath, scanProject);
        await _agentStatusEventPublisher.PublishAsync(new AgentCreatedEvent
        {
            AgentKey = verifierAgentKey,
            DisplayName = AgentStatusCatalog.CreateProjectVerifierAgentDisplayName(),
            InitialStatus = AgentStatusCatalog.WaitingStatus,
            GroupKey = groupKey,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken).ConfigureAwait(false);

        List<ChatMessage> planMessages = CreatePlanMessages(messageTemplates, scanProject);

        int planAttempts = 0;
        int verifierAttempts = 0;
        int verifierRejectionAttempts = 0;
        int missingSubmissionAttempts = 0;
        int projectPlanAgentResetCount = 0;

        while (true)
        {
            planAttempts++;
            await PublishStatusAsync(groupKey, plannerAgentKey, AgentStatusCatalog.RunningStatus, cancellationToken).ConfigureAwait(false);

            Result runPlanResult = await RunAgentAsync(projectPlanAgent, planMessages, cancellationToken).ConfigureAwait(false);

            if (runPlanResult.IsFailed)
            {
                await PublishStatusAsync(groupKey, plannerAgentKey, AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                return runPlanResult.ToResult<ProjectPlanWorkflowResult>();
            }

            await PublishStatusAsync(groupKey, plannerAgentKey, AgentStatusCatalog.CompletedStatus, cancellationToken).ConfigureAwait(false);

            IReadOnlyList<StoredProjectPlanTaskItem> taskItems =
                await _taskItemStore.ListAsync(cancellationToken).ConfigureAwait(false);

            if (taskItems.Count == 0)
            {
                missingSubmissionAttempts++;

                if (missingSubmissionAttempts >= _options.MaxMissingSubmissionAttempts)
                {
                    projectPlanAgentResetCount++;

                    if (projectPlanAgentResetCount > _options.MaxProjectPlanAgentResets)
                    {
                        await PublishStatusAsync(groupKey, plannerAgentKey, AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                        return Result.Fail<ProjectPlanWorkflowResult>(
                            "Project Plan Agent did not submit any task items after the allowed reset limit.");
                    }

                    projectPlanAgent = _projectPlanAgentFactory(repositoryRootPath);
                    await PublishStatusAsync(groupKey, plannerAgentKey, AgentStatusCatalog.WaitingStatus, cancellationToken).ConfigureAwait(false);
                    planMessages = CreatePlanMessages(messageTemplates, scanProject);
                    missingSubmissionAttempts = 0;
                    continue;
                }

                planMessages.Add(new ChatMessage(ChatRole.User, messageTemplates.MissingProjectPlanSubmissionMessage));
                continue;
            }

            missingSubmissionAttempts = 0;
            verifierAttempts++;
            _verdictBuffer.Reset();
            await PublishStatusAsync(groupKey, verifierAgentKey, AgentStatusCatalog.RunningStatus, cancellationToken).ConfigureAwait(false);

            List<ChatMessage> verifierMessages =
            [
                new(ChatRole.User, BuildVerifierInput(messageTemplates, taskItems)),
            ];

            Result runVerifierResult = await RunAgentAsync(projectVerifierAgent, verifierMessages, cancellationToken).ConfigureAwait(false);

            if (runVerifierResult.IsFailed)
            {
                await PublishStatusAsync(groupKey, verifierAgentKey, AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                return runVerifierResult.ToResult<ProjectPlanWorkflowResult>();
            }

            await PublishStatusAsync(groupKey, verifierAgentKey, AgentStatusCatalog.CompletedStatus, cancellationToken).ConfigureAwait(false);

            if (_verdictBuffer.Latest is not ReviewVerdict verdict)
            {
                await PublishStatusAsync(groupKey, verifierAgentKey, AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                return Result.Fail<ProjectPlanWorkflowResult>("Project Verifier Agent finished without submitting a verdict.");
            }

            if (verdict.Approved)
            {
                return Result.Ok(CreateResult(
                    scanProject,
                    taskItems,
                    verdict,
                    planAttempts,
                    verifierAttempts,
                    projectPlanAgentResetCount,
                    continuedAfterVerifierRejectionLimit: false));
            }

            verifierRejectionAttempts++;

            if (verifierRejectionAttempts >= _options.MaxVerifierRejectionAttempts)
            {
                await PublishStatusAsync(groupKey, verifierAgentKey, AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                return Result.Ok(CreateResult(
                    scanProject,
                    taskItems,
                    verdict,
                    planAttempts,
                    verifierAttempts,
                    projectPlanAgentResetCount,
                    continuedAfterVerifierRejectionLimit: true));
            }

            planMessages.Add(new ChatMessage(ChatRole.User, verdict.Message));
        }
    }

    private static ProjectPlanWorkflowResult CreateResult(
        StoredScanProject scanProject,
        IReadOnlyList<StoredProjectPlanTaskItem> taskItems,
        ReviewVerdict verdict,
        int planAttempts,
        int verifierAttempts,
        int projectPlanAgentResetCount,
        bool continuedAfterVerifierRejectionLimit) => new()
        {
            ScanProject = scanProject,
            TaskItems = taskItems,
            Verdict = verdict,
            ContinuedAfterVerifierRejectionLimit = continuedAfterVerifierRejectionLimit,
            PlanAttempts = planAttempts,
            VerifierAttempts = verifierAttempts,
            ProjectPlanAgentResetCount = projectPlanAgentResetCount,
        };

    private ValueTask PublishStatusAsync(
        string groupKey,
        string agentKey,
        string status,
        CancellationToken cancellationToken)
        => _agentStatusEventPublisher.PublishAsync(new AgentStatusChangedEvent
        {
            GroupKey = groupKey,
            AgentKey = agentKey,
            Status = status,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        }, cancellationToken);

    private static async Task<Result> RunAgentAsync(
        AIAgent agent,
        List<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        try
        {
            AgentResponse response = await agent.RunAsync(messages, session: null, options: null, cancellationToken).ConfigureAwait(false);

            foreach (ChatMessage message in response.Messages)
                messages.Add(message);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(new ExceptionalError($"Agent run failed: {ex}", ex));
        }
    }

    private static List<ChatMessage> CreatePlanMessages(
        ProjectPlanWorkflowMessageTemplates messageTemplates,
        StoredScanProject scanProject)
        =>
    [
        new(ChatRole.User, BuildPlanInput(messageTemplates, scanProject)),
    ];

    private static string BuildPlanInput(
        ProjectPlanWorkflowMessageTemplates messageTemplates,
        StoredScanProject scanProject)
        =>
        $"{messageTemplates.PlanInputPrefix}{Environment.NewLine}{Environment.NewLine}{JsonSerializer.Serialize(scanProject)}";

    private static string BuildVerifierInput(
        ProjectPlanWorkflowMessageTemplates messageTemplates,
        IReadOnlyList<StoredProjectPlanTaskItem> taskItems)
        =>
        $"{messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{JsonSerializer.Serialize(taskItems)}";
}
