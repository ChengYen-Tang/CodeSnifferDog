using CodeSnifferDog.Json;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Workflows.Common;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.ProjectPlan;

public sealed class ProjectPlanWorkflow(
    Func<string, IAgentEventScope, AgentCreationResult> projectPlanAgentFactory,
    Func<string, StoredScanProject, IAgentEventScope, AgentCreationResult> projectVerifierAgentFactory,
    IProjectPlanTaskItemStore taskItemStore,
    ReviewVerdictBuffer verdictBuffer,
    PromptAssetReader? promptAssetReader = null,
    ProjectPlanWorkflowOptions? options = null,
    IAgentEventBus? agentEventBus = null)
{
    private readonly Func<string, IAgentEventScope, AgentCreationResult> _projectPlanAgentFactory = projectPlanAgentFactory;
    private readonly Func<string, StoredScanProject, IAgentEventScope, AgentCreationResult> _projectVerifierAgentFactory = projectVerifierAgentFactory;
    private readonly IProjectPlanTaskItemStore _taskItemStore = taskItemStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader ?? new();
    private readonly ProjectPlanWorkflowOptions _options = options ?? new();
    private readonly IAgentEventBus _agentEventBus = agentEventBus ?? NoOpAgentEventBus.Instance;

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

        List<ChatMessage> planMessages = CreatePlanMessages(messageTemplates, scanProject);
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
                return runPlanResult.ToResult<ProjectPlanWorkflowResult>();

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
                        await plannerAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                        return Result.Fail<ProjectPlanWorkflowResult>(
                            "Project Plan Agent did not submit any task items after the allowed reset limit.");
                    }

                    projectPlanAgent = _projectPlanAgentFactory(repositoryRootPath, plannerAgentScope).Agent;
                    await plannerAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.WaitingStatus, cancellationToken).ConfigureAwait(false);
                    planMessages = CreatePlanMessages(messageTemplates, scanProject);
                    planPublishedMessageCount = 0;
                    missingSubmissionAttempts = 0;
                    continue;
                }

                planMessages.Add(new ChatMessage(ChatRole.User, messageTemplates.MissingProjectPlanSubmissionMessage));
                continue;
            }

            missingSubmissionAttempts = 0;
            verifierAttempts++;
            _verdictBuffer.Reset();

            List<ChatMessage> verifierMessages =
            [
                new(ChatRole.User, BuildVerifierInput(messageTemplates, taskItems)),
            ];
            int verifierPublishedMessageCount = 0;

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
                return runVerifierResult.ToResult<ProjectPlanWorkflowResult>();

            if (_verdictBuffer.Latest is not ReviewVerdict verdict)
            {
                await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
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
                await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
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

    private WorkflowAttemptLeasePair PrepareAttempt(Guid attemptId)
    {
        return new WorkflowAttemptLeasePair(
            _taskItemStore.BeginAttempt(attemptId),
            _verdictBuffer.BeginAttempt(attemptId));
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
        $"{messageTemplates.PlanInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(scanProject)}";

    private static string BuildVerifierInput(
        ProjectPlanWorkflowMessageTemplates messageTemplates,
        IReadOnlyList<StoredProjectPlanTaskItem> taskItems)
        =>
        $"{messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(taskItems)}";
}
