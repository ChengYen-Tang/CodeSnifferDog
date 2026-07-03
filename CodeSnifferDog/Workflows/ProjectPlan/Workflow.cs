using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Workflows.Common;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Workflows.ProjectPlan;

public sealed class Workflow(
    Func<string, IAgentEventScope, AgentCreationResult> AgentFactory,
    Func<string, StoredScanProject, IAgentEventScope, AgentCreationResult> VerifierFactory,
    ITaskItemStore taskItemStore,
    ReviewVerdictBuffer verdictBuffer,
    PromptAssetReader? promptAssetReader = null,
    WorkflowOptions? options = null,
    IAgentEventBus? agentEventBus = null)
{
    private readonly Func<string, IAgentEventScope, AgentCreationResult> _projectPlanAgentFactory = AgentFactory;
    private readonly Func<string, StoredScanProject, IAgentEventScope, AgentCreationResult> _projectVerifierAgentFactory = VerifierFactory;
    private readonly ITaskItemStore _taskItemStore = taskItemStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader ?? new();
    private readonly WorkflowOptions _options = options ?? new();
    private readonly IAgentEventBus _agentEventBus = agentEventBus ?? NoOpAgentEventBus.Instance;

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
                cancellationToken).ConfigureAwait(false);

            if (runPlanResult.IsFailed)
                return runPlanResult.ToResult<WorkflowResult>();

            IReadOnlyList<StoredTaskItem> taskItems =
                await _taskItemStore.ListAsync(cancellationToken).ConfigureAwait(false);

            if (taskItems.Count == 0)
            {
                missingSubmissionAttempts++;

                if (RetryLimit.IsReached(missingSubmissionAttempts, _options.MaxMissingSubmissionAttempts))
                {
                    projectPlanAgentResetCount++;

                    if (RetryLimit.IsExceeded(projectPlanAgentResetCount, _options.MaxProjectPlanAgentResets))
                    {
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
            List<ChatMessage> verifierMessages = messageBuilder.CreateVerifierMessages(taskItems);
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
                    cancellationToken).ConfigureAwait(false);

                if (runVerifierResult.IsFailed)
                    return runVerifierResult.ToResult<WorkflowResult>();

                if (_verdictBuffer.Latest is not ReviewVerdict verdict)
                {
                    verifierMissingVerdictAttempts++;

                    if (RetryLimit.IsReached(verifierMissingVerdictAttempts, _options.MaxMissingSubmissionAttempts))
                    {
                        await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                        return Result.Fail<WorkflowResult>("Project Verifier Agent finished without submitting a verdict.");
                    }

                    verifierMessages.Add(messageBuilder.CreateMissingVerifierVerdictMessage());
                    continue;
                }

                if (verdict.Approved)
                {
                    return Result.Ok(ResultFactory.Create(
                        scanProject,
                        taskItems,
                        verdict,
                        planAttempts,
                        verifierAttempts,
                        projectPlanAgentResetCount,
                        continuedAfterVerifierRejectionLimit: false));
                }

                verifierRejectionAttempts++;

                if (RetryLimit.IsReached(verifierRejectionAttempts, _options.MaxVerifierRejectionAttempts))
                {
                    await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                    return Result.Ok(ResultFactory.Create(
                        scanProject,
                        taskItems,
                        verdict,
                        planAttempts,
                        verifierAttempts,
                        projectPlanAgentResetCount,
                        continuedAfterVerifierRejectionLimit: true));
                }

                planMessages.Add(new ChatMessage(ChatRole.User, verdict.Message));
                break;
            }
        }
    }

    private WorkflowAttemptLeasePair PrepareAttempt(Guid attemptId)
    {
        return new WorkflowAttemptLeasePair(
            _taskItemStore.BeginAttempt(attemptId),
            _verdictBuffer.BeginAttempt(attemptId));
    }

}
