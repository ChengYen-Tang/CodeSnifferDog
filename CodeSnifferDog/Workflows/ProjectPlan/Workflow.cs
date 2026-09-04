using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.ProjectPlan.Listing;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Models.ProjectPlan.Tools.Listing;
using CodeSnifferDog.Workflows.Common;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Workflows.ProjectPlan;

/// <summary>
/// Runs project planning with a planner agent, a verifier agent, retry limits, and planner-reset handling.
/// </summary>
/// <param name="AgentFactory">Creates the project planner agent for one repository and event scope.</param>
/// <param name="VerifierFactory">Creates the verifier agent for one scanned project.</param>
/// <param name="taskItemStore">Store that receives project-plan task item submissions.</param>
/// <param name="verdictBuffer">Buffer that captures verifier verdict submissions.</param>
/// <param name="promptAssetReader">Optional prompt reader used to load workflow prompt assets.</param>
/// <param name="options">Optional workflow options that control retries and timeouts.</param>
/// <param name="agentEventBus">Optional event bus used to publish agent lifecycle and transcript events.</param>
public sealed class Workflow(
    Func<string, IAgentEventScope, AgentCreationResult> AgentFactory,
    Func<string, StoredScanProject, IAgentEventScope, AgentCreationResult> VerifierFactory,
    ITaskItemStore taskItemStore,
    ReviewVerdictBuffer verdictBuffer,
    PromptAssetReader? promptAssetReader = null,
    WorkflowOptions? options = null,
    IAgentEventBus? agentEventBus = null,
    ILogger? logger = null)
{
    private readonly Func<string, IAgentEventScope, AgentCreationResult> _projectPlanAgentFactory = AgentFactory;
    private readonly Func<string, StoredScanProject, IAgentEventScope, AgentCreationResult> _projectVerifierAgentFactory = VerifierFactory;
    private readonly ITaskItemStore _taskItemStore = taskItemStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader ?? new();
    private readonly WorkflowOptions _options = options ?? new();
    private readonly IAgentEventBus _agentEventBus = agentEventBus ?? NoOpAgentEventBus.Instance;
    private readonly ILogger? _logger = logger;

    /// <summary>
    /// Runs the project-plan workflow for one scanned project.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path that contains the scanned project.</param>
    /// <param name="scanProject">Scanned project that should be decomposed into task items.</param>
    /// <param name="cancellationToken">Cancels the workflow.</param>
    /// <returns>The project-plan workflow result.</returns>
    public async Task<Result<WorkflowResult>> RunAsync(
        string repositoryRootPath,
        StoredScanProject scanProject,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<WorkflowResult>("Repository root path is required.");

        ArgumentNullException.ThrowIfNull(scanProject);

        repositoryRootPath = repositoryRootPath.Trim();
        await _taskItemStore.ClearAsync(cancellationToken).ConfigureAwait(false);

        MessageTemplates messageTemplates = new(_promptAssetReader);
        MessageBuilder messageBuilder = new(messageTemplates);
        string groupKey = AgentStatusCatalog.CreateProjectPlanGroupKey(scanProject);
        IAgentEventScope plannerAgentScope = _agentEventBus.CreateScope(groupKey, AgentStatusCatalog.CreateProjectPlannerAgentKey(scanProject));
        IAgentEventScope verifierAgentScope = _agentEventBus.CreateScope(groupKey, AgentStatusCatalog.CreateProjectVerifierAgentKey(scanProject));

        await _agentEventBus.PublishGroupCreatedAsync(
            groupKey,
            AgentStatusCatalog.CreateProjectPlanGroupDisplayName(scanProject),
            cancellationToken).ConfigureAwait(false);

        AgentCreationResult projectPlanAgentCreation = _projectPlanAgentFactory(repositoryRootPath, plannerAgentScope);
        AIAgent projectPlanAgent = projectPlanAgentCreation.Agent;
        await plannerAgentScope.PublishCreatedAsync(
            AgentStatusCatalog.CreateProjectPlannerAgentDisplayName(),
            projectPlanAgentCreation.SystemPrompt,
            AgentStatusCatalog.WaitingStatus,
            cancellationToken).ConfigureAwait(false);

        AgentCreationResult projectVerifierAgentCreation = _projectVerifierAgentFactory(repositoryRootPath, scanProject, verifierAgentScope);
        AIAgent projectVerifierAgent = projectVerifierAgentCreation.Agent;
        await verifierAgentScope.PublishCreatedAsync(
            AgentStatusCatalog.CreateProjectVerifierAgentDisplayName(),
            projectVerifierAgentCreation.SystemPrompt,
            AgentStatusCatalog.WaitingStatus,
            cancellationToken).ConfigureAwait(false);

        List<ChatMessage> planMessages = messageBuilder.CreatePlanMessages(scanProject);
        int planPublishedMessageCount = 0;

        int planAttempts = 0;
        int verifierAttempts = 0;
        int verifierRejectionAttempts = 0;
        int missingSubmissionAttempts = 0;
        int projectPlanAgentResetCount = 0;

        while (true)
        {
            planAttempts++;

            (Result runPlanResult, planPublishedMessageCount, projectPlanAgent) = await WorkflowAgentRunService.RunAsync(
                projectPlanAgent,
                () => _projectPlanAgentFactory(repositoryRootPath, plannerAgentScope).Agent,
                PrepareAttempt,
                static state => state.Restore(),
                planMessages,
                plannerAgentScope,
                planPublishedMessageCount,
                _options.AgentRunTimeout,
                _options.MaxConsecutiveRunFailures,
                cancellationToken,
                _logger).ConfigureAwait(false);

            if (runPlanResult.IsFailed)
                return runPlanResult.ToResult<WorkflowResult>();

            IReadOnlyList<StoredTaskItem> verifierTaskItems = await _taskItemStore.ListPageAsync(
                cursor: null,
                take: TaskItemPage.DefaultPageSize + 1,
                cancellationToken).ConfigureAwait(false);

            if (verifierTaskItems.Count == 0)
            {
                missingSubmissionAttempts++;

                if (RetryLimit.IsReached(missingSubmissionAttempts, _options.MaxMissingSubmissionAttempts))
                {
                    projectPlanAgentResetCount++;

                    if (RetryLimit.IsExceeded(projectPlanAgentResetCount, _options.MaxProjectPlanAgentResets))
                    {
                        _logger?.LogError(
                            "Project plan agent failed to submit task items after the allowed reset limit. GroupKey: {AgentGroupKey}; AgentKey: {AgentKey}; ProjectName: {ProjectName}; Attempts: {Attempts}; ResetCount: {ResetCount}",
                            plannerAgentScope.GroupKey,
                            plannerAgentScope.AgentKey,
                            scanProject.ProjectName,
                            planAttempts,
                            projectPlanAgentResetCount);
                        await plannerAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                        return Result.Fail<WorkflowResult>(
                            "Project Plan Agent did not submit any task items after the allowed reset limit.");
                    }

                    projectPlanAgent = _projectPlanAgentFactory(repositoryRootPath, plannerAgentScope).Agent;
                    await plannerAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.WaitingStatus, cancellationToken).ConfigureAwait(false);
                    planMessages = messageBuilder.CreatePlanMessages(scanProject);
                    planPublishedMessageCount = 0;
                    missingSubmissionAttempts = 0;
                    continue;
                }

                planMessages.Add(messageBuilder.CreateMissingSubmissionMessage());
                continue;
            }

            missingSubmissionAttempts = 0;
            TaskItemPage taskItemPage = TaskItemPageFactory.Create(verifierTaskItems, TaskItemPage.DefaultPageSize);
            List<ChatMessage> verifierMessages = messageBuilder.CreateVerifierMessages(taskItemPage);
            int verifierPublishedMessageCount = 0;
            int verifierMissingVerdictAttempts = 0;

            while (true)
            {
                verifierAttempts++;
                _verdictBuffer.Reset();

                (Result runVerifierResult, verifierPublishedMessageCount, projectVerifierAgent) = await WorkflowAgentRunService.RunAsync(
                    projectVerifierAgent,
                    () => _projectVerifierAgentFactory(repositoryRootPath, scanProject, verifierAgentScope).Agent,
                    PrepareAttempt,
                    static state => state.Restore(),
                    verifierMessages,
                    verifierAgentScope,
                    verifierPublishedMessageCount,
                    _options.AgentRunTimeout,
                    _options.MaxConsecutiveRunFailures,
                    cancellationToken,
                    _logger).ConfigureAwait(false);

                if (runVerifierResult.IsFailed)
                    return runVerifierResult.ToResult<WorkflowResult>();

                if (_verdictBuffer.Latest is not ReviewVerdict verdict)
                {
                    verifierMissingVerdictAttempts++;

                    if (RetryLimit.IsReached(verifierMissingVerdictAttempts, _options.MaxMissingSubmissionAttempts))
                    {
                        _logger?.LogError(
                            "Project verifier agent failed to submit a verdict after the allowed attempts. GroupKey: {AgentGroupKey}; AgentKey: {AgentKey}; ProjectName: {ProjectName}; Attempts: {Attempts}",
                            verifierAgentScope.GroupKey,
                            verifierAgentScope.AgentKey,
                            scanProject.ProjectName,
                            verifierMissingVerdictAttempts);
                        await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                        return Result.Fail<WorkflowResult>("Project Verifier Agent finished without submitting a verdict.");
                    }

                    verifierMessages.Add(messageBuilder.CreateMissingVerifierVerdictMessage());
                    continue;
                }

                if (verdict.Approved)
                {
                    return Result.Ok(await CreateResultAsync(
                        scanProject,
                        verdict,
                        planAttempts,
                        verifierAttempts,
                        projectPlanAgentResetCount,
                        continuedAfterVerifierRejectionLimit: false,
                        cancellationToken).ConfigureAwait(false));
                }

                verifierRejectionAttempts++;

                if (RetryLimit.IsReached(verifierRejectionAttempts, _options.MaxVerifierRejectionAttempts))
                {
                    await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                    return Result.Ok(await CreateResultAsync(
                        scanProject,
                        verdict,
                        planAttempts,
                        verifierAttempts,
                        projectPlanAgentResetCount,
                        continuedAfterVerifierRejectionLimit: true,
                        cancellationToken).ConfigureAwait(false));
                }

                planMessages.Add(new ChatMessage(ChatRole.User, verdict.Message));
                break;
            }
        }
    }

    /// <summary>
    /// Creates the final project-plan result from the complete stored task-item set after verification concludes.
    /// </summary>
    private async ValueTask<WorkflowResult> CreateResultAsync(
        StoredScanProject scanProject,
        ReviewVerdict verdict,
        int planAttempts,
        int verifierAttempts,
        int projectPlanAgentResetCount,
        bool continuedAfterVerifierRejectionLimit,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<StoredTaskItem> taskItems = await _taskItemStore.ListAllAsync(cancellationToken).ConfigureAwait(false);
        return ResultFactory.Create(
            scanProject,
            taskItems,
            verdict,
            planAttempts,
            verifierAttempts,
            projectPlanAgentResetCount,
            continuedAfterVerifierRejectionLimit);
    }

    /// <summary>
    /// Captures restorable state for one project-plan workflow attempt.
    /// </summary>
    /// <param name="attemptId">Attempt identifier associated with the snapshot.</param>
    /// <returns>A lease pair that can restore both the task-item store and verdict buffer.</returns>
    private WorkflowAttemptLeasePair PrepareAttempt(Guid attemptId)
    {
        return new WorkflowAttemptLeasePair(
            _taskItemStore.BeginAttempt(attemptId),
            _verdictBuffer.BeginAttempt(attemptId));
    }

}
