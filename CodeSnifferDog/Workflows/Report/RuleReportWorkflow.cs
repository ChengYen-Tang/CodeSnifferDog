using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Workflows.Common;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.Report;

public sealed class RuleReportWorkflow(
    Func<string, string, string, StoredProjectPlanTaskItem, IAgentEventScope, AgentCreationResult> reportAggregatorAgentFactory,
    Func<string, string, string, StoredProjectPlanTaskItem, IReadOnlyList<StoredRuleReviewIssue>, IAgentEventScope, AgentCreationResult> reportVerifierAgentFactory,
    IRuleReportIssueStore reportIssueStore,
    ReviewVerdictBuffer verdictBuffer,
    PromptAssetReader? promptAssetReader = null,
    RuleReportWorkflowOptions? options = null,
    IAgentEventBus? agentEventBus = null)
{
    private readonly Func<string, string, string, StoredProjectPlanTaskItem, IAgentEventScope, AgentCreationResult> _reportAggregatorAgentFactory = reportAggregatorAgentFactory;
    private readonly Func<string, string, string, StoredProjectPlanTaskItem, IReadOnlyList<StoredRuleReviewIssue>, IAgentEventScope, AgentCreationResult> _reportVerifierAgentFactory = reportVerifierAgentFactory;
    private readonly IRuleReportIssueStore _reportIssueStore = reportIssueStore;
    private readonly RuleReportDiffService _diffService = new(reportIssueStore);
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly RuleReportWorkflowMessageBuilder _messageBuilder =
        new(new RuleReportWorkflowMessageTemplates(promptAssetReader ?? new PromptAssetReader()));
    private readonly RuleReportWorkflowOptions _options = options ?? new();
    private readonly IAgentEventBus _agentEventBus = agentEventBus ?? NoOpAgentEventBus.Instance;
    private RuleFlowKey _ruleFlowKey = default!;
    private string _reportVerdictScopeKey = string.Empty;

    public async Task<Result<RuleReportWorkflowResult>> RunAsync(
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<RuleReportWorkflowResult>("Repository root path is required.");

        if (string.IsNullOrWhiteSpace(ruleMarkdown))
            return Result.Fail<RuleReportWorkflowResult>("Rule markdown is required.");

        if (string.IsNullOrWhiteSpace(ruleKey))
            return Result.Fail<RuleReportWorkflowResult>("Rule key is required.");

        ArgumentNullException.ThrowIfNull(taskItem);
        ArgumentNullException.ThrowIfNull(currentFlowIssues);

        if (currentFlowIssues.Count == 0)
            return Result.Fail<RuleReportWorkflowResult>("Current flow issues are required for report aggregation.");

        repositoryRootPath = repositoryRootPath.Trim();
        ruleKey = ruleKey.Trim();
        ruleMarkdown = ruleMarkdown.Trim();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, ruleKey);
        RuleReportKey ruleReportKey = RuleScopeKeyFactory.CreateRuleReportKey(repositoryRootPath, ruleKey);
        string reportVerdictScopeKey = RuleScopeKeyFactory.CreateReportVerdictScopeKey(ruleFlowKey);
        _ruleFlowKey = ruleFlowKey;
        _reportVerdictScopeKey = reportVerdictScopeKey;
        await _reportIssueStore.InitializeWorkingReportAsync(ruleReportKey, ruleKey, ruleFlowKey, cancellationToken).ConfigureAwait(false);
        _verdictBuffer.Reset(reportVerdictScopeKey);

        try
        {
            string groupKey = AgentStatusCatalog.CreateReviewTaskGroupKey(taskItem);
            IAgentEventScope aggregatorAgentScope = _agentEventBus.CreateScope(groupKey, AgentStatusCatalog.CreateReportAggregatorAgentKey(taskItem, ruleKey));
            IAgentEventScope verifierAgentScope = _agentEventBus.CreateScope(groupKey, AgentStatusCatalog.CreateReportVerifierAgentKey(taskItem, ruleKey));

            Result<AgentCreationResult> createAggregatorResult = await WorkflowAgentLifecycle.CreateAndPublishAsync(
                () => _reportAggregatorAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, aggregatorAgentScope),
                "Report Aggregator Agent",
                aggregatorAgentScope,
                AgentStatusCatalog.CreateReportAggregatorAgentDisplayName(ruleKey),
                cancellationToken).ConfigureAwait(false);

            if (createAggregatorResult.IsFailed)
                return createAggregatorResult.ToResult<RuleReportWorkflowResult>();

            Result<AgentCreationResult> createVerifierResult = await WorkflowAgentLifecycle.CreateAndPublishAsync(
                () => _reportVerifierAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, currentFlowIssues, verifierAgentScope),
                "Report Verifier Agent",
                verifierAgentScope,
                AgentStatusCatalog.CreateReportVerifierAgentDisplayName(ruleKey),
                cancellationToken).ConfigureAwait(false);

            if (createVerifierResult.IsFailed)
                return createVerifierResult.ToResult<RuleReportWorkflowResult>();

            AIAgent reportAggregatorAgent = createAggregatorResult.Value.Agent;
            AIAgent reportVerifierAgent = createVerifierResult.Value.Agent;
            List<ChatMessage> aggregatorMessages = _messageBuilder.CreateAggregatorMessages(currentFlowIssues);
            int aggregatorPublishedMessageCount = 0;

            int aggregatorAttempts = 0;
            int verifierAttempts = 0;
            int verifierRejectionAttempts = 0;

            while (true)
            {
                aggregatorAttempts++;

                (Result runAggregatorResult, aggregatorPublishedMessageCount, reportAggregatorAgent) = await WorkflowAgentRunService.RunAsync(
                    reportAggregatorAgent,
                    () => _reportAggregatorAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, aggregatorAgentScope).Agent,
                    PrepareAttempt,
                    static state => state.Restore(),
                    aggregatorMessages,
                    aggregatorAgentScope,
                    aggregatorPublishedMessageCount,
                    _options.AgentRunTimeout,
                    _options.MaxConsecutiveRunFailures,
                    cancellationToken).ConfigureAwait(false);

                if (runAggregatorResult.IsFailed)
                    return runAggregatorResult.ToResult<RuleReportWorkflowResult>();

                RuleReportDiff diff = await _diffService.ComputeAndStoreDiffAsync(ruleReportKey, ruleFlowKey, cancellationToken).ConfigureAwait(false);

                verifierAttempts++;
                _verdictBuffer.Reset(reportVerdictScopeKey);

                List<ChatMessage> verifierMessages = _messageBuilder.CreateVerifierMessages(diff);
                int verifierPublishedMessageCount = 0;

                (Result runVerifierResult, verifierPublishedMessageCount, reportVerifierAgent) = await WorkflowAgentRunService.RunAsync(
                    reportVerifierAgent,
                    () => _reportVerifierAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, currentFlowIssues, verifierAgentScope).Agent,
                    PrepareAttempt,
                    static state => state.Restore(),
                    verifierMessages,
                    verifierAgentScope,
                    verifierPublishedMessageCount,
                    _options.AgentRunTimeout,
                    _options.MaxConsecutiveRunFailures,
                    cancellationToken).ConfigureAwait(false);

                if (runVerifierResult.IsFailed)
                    return runVerifierResult.ToResult<RuleReportWorkflowResult>();

                if (_verdictBuffer.GetLatest(reportVerdictScopeKey) is not ReviewVerdict verdict)
                {
                    await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                    return Result.Fail<RuleReportWorkflowResult>("Report Verifier Agent finished without submitting a verdict.");
                }

                if (verdict.Approved)
                {
                    await _reportIssueStore.PromoteWorkingReportAsync(ruleReportKey, ruleFlowKey, cancellationToken).ConfigureAwait(false);
                    IReadOnlyList<StoredRuleReportIssue> repositoryIssues =
                        await _reportIssueStore.GetLatestSnapshotAsync(ruleReportKey, cancellationToken).ConfigureAwait(false);

                    return Result.Ok(RuleReportWorkflowResultFactory.Create(
                        ruleKey,
                        taskItem,
                        diff,
                        repositoryIssues,
                        verdict,
                        continuedAfterVerifierRejectionLimit: false,
                        aggregatorAttempts,
                        verifierAttempts));
                }

                verifierRejectionAttempts++;

                if (verifierRejectionAttempts >= _options.MaxVerifierRejectionAttempts)
                {
                    await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                    await _reportIssueStore.PromoteWorkingReportAsync(ruleReportKey, ruleFlowKey, cancellationToken).ConfigureAwait(false);
                    IReadOnlyList<StoredRuleReportIssue> repositoryIssues =
                        await _reportIssueStore.GetLatestSnapshotAsync(ruleReportKey, cancellationToken).ConfigureAwait(false);

                    return Result.Ok(RuleReportWorkflowResultFactory.Create(
                        ruleKey,
                        taskItem,
                        diff,
                        repositoryIssues,
                        verdict,
                        continuedAfterVerifierRejectionLimit: true,
                        aggregatorAttempts,
                        verifierAttempts));
                }

                aggregatorMessages.Add(new ChatMessage(ChatRole.User, verdict.Message));
            }
        }
        finally
        {
            _verdictBuffer.Reset(reportVerdictScopeKey);
            await _reportIssueStore.ClearWorkingReportAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);
        }
    }

    private WorkflowAttemptLeasePair PrepareAttempt(Guid attemptId)
    {
        return new WorkflowAttemptLeasePair(
            _reportIssueStore.BeginAttempt(_ruleFlowKey, attemptId),
            _verdictBuffer.BeginAttempt(_reportVerdictScopeKey, attemptId));
    }

}
