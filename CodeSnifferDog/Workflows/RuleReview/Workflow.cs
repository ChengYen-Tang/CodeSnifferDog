using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Workflows.Common;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;
using StoredTaskItem = CodeSnifferDog.Models.ProjectPlan.StoredTaskItem;
using RuleReviewWorkflowOptions = CodeSnifferDog.Models.RuleReview.WorkflowOptions;
using RuleReviewWorkflowResult = CodeSnifferDog.Models.RuleReview.WorkflowResult;

namespace CodeSnifferDog.Workflows.RuleReview;

public sealed class Workflow(
    Func<string, string, string, StoredTaskItem, IAgentEventScope, AgentCreationResult> AgentFactory,
    Func<string, string, string, StoredTaskItem, IAgentEventScope, AgentCreationResult> VerifierFactory,
    IIssueStore issueStore,
    ReviewVerdictBuffer verdictBuffer,
    PromptAssetReader? promptAssetReader = null,
    RuleReviewWorkflowOptions? options = null,
    IAgentEventBus? agentEventBus = null)
{
    private readonly Func<string, string, string, StoredTaskItem, IAgentEventScope, AgentCreationResult> _ruleReviewAgentFactory = AgentFactory;
    private readonly Func<string, string, string, StoredTaskItem, IAgentEventScope, AgentCreationResult> _reviewVerifierAgentFactory = VerifierFactory;
    private readonly IIssueStore _issueStore = issueStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly MessageBuilder _messageBuilder =
        new(new MessageTemplates(promptAssetReader ?? new PromptAssetReader()));
    private readonly RuleReviewWorkflowOptions _options = options ?? new();
    private readonly IAgentEventBus _agentEventBus = agentEventBus ?? NoOpAgentEventBus.Instance;
    private RuleFlowKey _ruleFlowKey = default!;
    private string _reviewVerdictScopeKey = string.Empty;

    public async Task<Result<RuleReviewWorkflowResult>> RunAsync(
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredTaskItem taskItem,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<RuleReviewWorkflowResult>("Repository root path is required.");

        if (string.IsNullOrWhiteSpace(ruleMarkdown))
            return Result.Fail<RuleReviewWorkflowResult>("Rule markdown is required.");

        if (string.IsNullOrWhiteSpace(ruleKey))
            return Result.Fail<RuleReviewWorkflowResult>("Rule key is required.");

        ArgumentNullException.ThrowIfNull(taskItem);

        repositoryRootPath = repositoryRootPath.Trim();
        ruleKey = ruleKey.Trim();
        ruleMarkdown = ruleMarkdown.Trim();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, ruleKey);
        string reviewVerdictScopeKey = RuleScopeKeyFactory.CreateReviewVerdictScopeKey(ruleFlowKey);
        _ruleFlowKey = ruleFlowKey;
        _reviewVerdictScopeKey = reviewVerdictScopeKey;
        await _issueStore.ClearAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);
        _verdictBuffer.Reset(reviewVerdictScopeKey);

        try
        {
            string groupKey = AgentStatusCatalog.CreateReviewTaskGroupKey(taskItem);
            IAgentEventScope reviewAgentScope = _agentEventBus.CreateScope(groupKey, AgentStatusCatalog.CreateRuleReviewAgentKey(taskItem, ruleKey));
            IAgentEventScope verifierAgentScope = _agentEventBus.CreateScope(groupKey, AgentStatusCatalog.CreateReviewVerifierAgentKey(taskItem, ruleKey));

            Result<AgentCreationResult> createRuleReviewAgentResult = await WorkflowAgentLifecycle.CreateAndPublishAsync(
                () => _ruleReviewAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, reviewAgentScope),
                "Rule Review Agent",
                reviewAgentScope,
                AgentStatusCatalog.CreateRuleReviewAgentDisplayName(ruleKey),
                cancellationToken).ConfigureAwait(false);

            if (createRuleReviewAgentResult.IsFailed)
                return createRuleReviewAgentResult.ToResult<WorkflowResult>();

            Result<AgentCreationResult> createReviewVerifierAgentResult = await WorkflowAgentLifecycle.CreateAndPublishAsync(
                () => _reviewVerifierAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, verifierAgentScope),
                "Review Verifier Agent",
                verifierAgentScope,
                AgentStatusCatalog.CreateReviewVerifierAgentDisplayName(ruleKey),
                cancellationToken).ConfigureAwait(false);

            if (createReviewVerifierAgentResult.IsFailed)
                return createReviewVerifierAgentResult.ToResult<WorkflowResult>();

            AIAgent ruleReviewAgent = createRuleReviewAgentResult.Value.Agent;
            AIAgent reviewVerifierAgent = createReviewVerifierAgentResult.Value.Agent;
            List<ChatMessage> reviewMessages = _messageBuilder.CreateReviewMessages();
            int reviewPublishedMessageCount = 0;

            int reviewAttempts = 0;
            int verifierAttempts = 0;
            int verifierRejectionAttempts = 0;
            int missingSubmissionAttempts = 0;
            int ruleReviewAgentResetCount = 0;

            while (true)
            {
                reviewAttempts++;

                (Result runReviewResult, reviewPublishedMessageCount, ruleReviewAgent) = await WorkflowAgentRunService.RunAsync(
                    ruleReviewAgent,
                    () => _ruleReviewAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, reviewAgentScope).Agent,
                    PrepareAttempt,
                    static state => state.Restore(),
                    reviewMessages,
                    reviewAgentScope,
                    reviewPublishedMessageCount,
                    _options.AgentRunTimeout,
                    _options.MaxConsecutiveRunFailures,
                    cancellationToken).ConfigureAwait(false);

                if (runReviewResult.IsFailed)
                    return runReviewResult.ToResult<WorkflowResult>();

                IReadOnlyList<StoredIssue> issues = await _issueStore.ListAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);
                NoIssueConclusion? noIssueConclusion = await _issueStore.GetNoIssueConclusionAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);

                if (issues.Count == 0 && noIssueConclusion is null)
                {
                    missingSubmissionAttempts++;

                    if (RetryLimit.IsReached(missingSubmissionAttempts, _options.MaxMissingSubmissionAttempts))
                    {
                        ruleReviewAgentResetCount++;

                        if (RetryLimit.IsExceeded(ruleReviewAgentResetCount, _options.MaxRuleReviewAgentResets))
                        {
                            await reviewAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                            ReviewVerdict missingSubmissionVerdict = new()
                            {
                                Approved = false,
                                Message = "Rule Review Agent did not submit any issues or a no-issue conclusion after the allowed reset limit.",
                            };

                            return Result.Ok(ResultFactory.Create(
                                taskItem,
                                ruleKey,
                                issues,
                                noIssueConclusion,
                                missingSubmissionVerdict,
                                reviewAttempts,
                                verifierAttempts,
                                ruleReviewAgentResetCount,
                                continuedAfterVerifierRejectionLimit: false,
                                stoppedAfterMissingSubmissionLimit: true));
                        }

                        Result<AgentCreationResult> recreateRuleReviewAgentResult = WorkflowAgentCreation.TryCreate(
                            () => _ruleReviewAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, reviewAgentScope),
                            "Rule Review Agent");

                        if (recreateRuleReviewAgentResult.IsFailed)
                            return recreateRuleReviewAgentResult.ToResult<WorkflowResult>();

                        ruleReviewAgent = recreateRuleReviewAgentResult.Value.Agent;
                        await reviewAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.WaitingStatus, cancellationToken).ConfigureAwait(false);
                        reviewMessages = _messageBuilder.CreateReviewMessages();
                        reviewPublishedMessageCount = 0;
                        missingSubmissionAttempts = 0;
                        continue;
                    }

                    reviewMessages.Add(_messageBuilder.CreateMissingSubmissionMessage());
                    continue;
                }

                missingSubmissionAttempts = 0;
                List<ChatMessage> verifierMessages = _messageBuilder.CreateVerifierMessages(issues, noIssueConclusion);
                int verifierPublishedMessageCount = 0;
                int verifierMissingVerdictAttempts = 0;

                while (true)
                {
                    verifierAttempts++;
                    _verdictBuffer.Reset(reviewVerdictScopeKey);

                    (Result runVerifierResult, verifierPublishedMessageCount, reviewVerifierAgent) = await WorkflowAgentRunService.RunAsync(
                        reviewVerifierAgent,
                        () => _reviewVerifierAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, verifierAgentScope).Agent,
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

                    if (_verdictBuffer.GetLatest(reviewVerdictScopeKey) is not ReviewVerdict verdict)
                    {
                        verifierMissingVerdictAttempts++;

                        if (RetryLimit.IsReached(verifierMissingVerdictAttempts, _options.MaxMissingSubmissionAttempts))
                        {
                            await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                            return Result.Fail<WorkflowResult>("Review Verifier Agent finished without submitting a verdict.");
                        }

                        verifierMessages.Add(_messageBuilder.CreateMissingVerifierVerdictMessage());
                        continue;
                    }

                    if (verdict.Approved)
                    {
                        return Result.Ok(ResultFactory.Create(
                            taskItem,
                            ruleKey,
                            issues,
                            noIssueConclusion,
                            verdict,
                            reviewAttempts,
                            verifierAttempts,
                            ruleReviewAgentResetCount,
                            continuedAfterVerifierRejectionLimit: false,
                            stoppedAfterMissingSubmissionLimit: false));
                    }

                    verifierRejectionAttempts++;

                    if (RetryLimit.IsReached(verifierRejectionAttempts, _options.MaxVerifierRejectionAttempts))
                    {
                        await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                        return Result.Ok(ResultFactory.Create(
                            taskItem,
                            ruleKey,
                            issues,
                            noIssueConclusion,
                            verdict,
                            reviewAttempts,
                            verifierAttempts,
                            ruleReviewAgentResetCount,
                            continuedAfterVerifierRejectionLimit: true,
                            stoppedAfterMissingSubmissionLimit: false));
                    }

                    reviewMessages.Add(new ChatMessage(ChatRole.User, verdict.Message));
                    break;
                }
            }
        }
        finally
        {
            _verdictBuffer.Reset(reviewVerdictScopeKey);
            await _issueStore.ClearAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);
        }
    }

    private WorkflowAttemptLeasePair PrepareAttempt(Guid attemptId)
    {
        return new WorkflowAttemptLeasePair(
            _issueStore.BeginAttempt(_ruleFlowKey, attemptId),
            _verdictBuffer.BeginAttempt(_reviewVerdictScopeKey, attemptId));
    }

}
