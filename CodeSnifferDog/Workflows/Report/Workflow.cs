using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Workflows.Common;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;
using ReportWorkflowOptions = CodeSnifferDog.Models.Report.WorkflowOptions;
using ReportWorkflowResult = CodeSnifferDog.Models.Report.WorkflowResult;
using RuleReviewStoredIssue = CodeSnifferDog.Models.RuleReview.StoredIssue;

namespace CodeSnifferDog.Workflows.Report;

/// <summary>
/// Runs report aggregation and verification for one reviewed rule flow.
/// </summary>
/// <param name="reportAggregatorAgentFactory">Creates the report aggregator agent for one task item and rule.</param>
/// <param name="reportVerifierAgentFactory">Creates the report verifier agent for one task item, rule, and current flow issues.</param>
/// <param name="reportIssueStore">Store that persists working and promoted report issues.</param>
/// <param name="verdictBuffer">Buffer that captures verifier verdict submissions.</param>
/// <param name="promptAssetReader">Optional prompt reader used to load workflow prompt assets.</param>
/// <param name="options">Optional workflow options that control retries and timeouts.</param>
/// <param name="agentEventBus">Optional event bus used to publish agent lifecycle and transcript events.</param>
public sealed class Workflow(
    Func<string, string, string, StoredTaskItem, IAgentEventScope, AgentCreationResult> reportAggregatorAgentFactory,
    Func<string, string, string, StoredTaskItem, IReadOnlyList<RuleReviewStoredIssue>, IAgentEventScope, AgentCreationResult> reportVerifierAgentFactory,
    IIssueStore reportIssueStore,
    ReviewVerdictBuffer verdictBuffer,
    PromptAssetReader? promptAssetReader = null,
    ReportWorkflowOptions? options = null,
    IAgentEventBus? agentEventBus = null,
    ILogger? logger = null)
{
    private readonly Func<string, string, string, StoredTaskItem, IAgentEventScope, AgentCreationResult> _reportAggregatorAgentFactory = reportAggregatorAgentFactory;
    private readonly Func<string, string, string, StoredTaskItem, IReadOnlyList<RuleReviewStoredIssue>, IAgentEventScope, AgentCreationResult> _reportVerifierAgentFactory = reportVerifierAgentFactory;
    private readonly IIssueStore _reportIssueStore = reportIssueStore;
    private readonly DiffService _diffService = new(reportIssueStore);
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly MessageBuilder _messageBuilder =
        new(new MessageTemplates(promptAssetReader ?? new PromptAssetReader()));
    private readonly ReportWorkflowOptions _options = options ?? new();
    private readonly IAgentEventBus _agentEventBus = agentEventBus ?? NoOpAgentEventBus.Instance;
    private readonly ILogger? _logger = logger;
    private RuleFlowKey _ruleFlowKey = default!;
    private string _reportVerdictScopeKey = string.Empty;

    /// <summary>
    /// Runs the report workflow for one reviewed rule flow.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path that contains the reviewed code.</param>
    /// <param name="ruleKey">Rule key whose report is being generated.</param>
    /// <param name="ruleMarkdown">Rendered rule guidance supplied to the agents.</param>
    /// <param name="taskItem">Task item whose rule flow produced the report.</param>
    /// <param name="currentFlowIssues">Current issues produced by the rule-review flow.</param>
    /// <param name="cancellationToken">Cancels the workflow.</param>
    /// <returns>The report workflow result.</returns>
    public async Task<Result<ReportWorkflowResult>> RunAsync(
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredTaskItem taskItem,
        IReadOnlyList<RuleReviewStoredIssue> currentFlowIssues,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<ReportWorkflowResult>("Repository root path is required.");

        if (string.IsNullOrWhiteSpace(ruleMarkdown))
            return Result.Fail<ReportWorkflowResult>("Rule markdown is required.");

        if (string.IsNullOrWhiteSpace(ruleKey))
            return Result.Fail<ReportWorkflowResult>("Rule key is required.");

        ArgumentNullException.ThrowIfNull(taskItem);
        ArgumentNullException.ThrowIfNull(currentFlowIssues);

        if (currentFlowIssues.Count == 0)
            return Result.Fail<ReportWorkflowResult>("Current flow issues are required for report aggregation.");

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
                return createAggregatorResult.ToResult<ReportWorkflowResult>();

            Result<AgentCreationResult> createVerifierResult = await WorkflowAgentLifecycle.CreateAndPublishAsync(
                () => _reportVerifierAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, currentFlowIssues, verifierAgentScope),
                "Report Verifier Agent",
                verifierAgentScope,
                AgentStatusCatalog.CreateReportVerifierAgentDisplayName(ruleKey),
                cancellationToken).ConfigureAwait(false);

            if (createVerifierResult.IsFailed)
                return createVerifierResult.ToResult<ReportWorkflowResult>();

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
                    cancellationToken,
                    _logger).ConfigureAwait(false);

                if (runAggregatorResult.IsFailed)
                    return runAggregatorResult.ToResult<ReportWorkflowResult>();

                Diff diff = await _diffService.ComputeAndStoreDiffAsync(ruleReportKey, ruleFlowKey, cancellationToken).ConfigureAwait(false);

                List<ChatMessage> verifierMessages = _messageBuilder.CreateVerifierMessages(diff);
                int verifierPublishedMessageCount = 0;
                int verifierMissingVerdictAttempts = 0;

                while (true)
                {
                    verifierAttempts++;
                    _verdictBuffer.Reset(reportVerdictScopeKey);

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
                        cancellationToken,
                        _logger).ConfigureAwait(false);

                    if (runVerifierResult.IsFailed)
                        return runVerifierResult.ToResult<ReportWorkflowResult>();

                    if (_verdictBuffer.GetLatest(reportVerdictScopeKey) is not ReviewVerdict verdict)
                    {
                        verifierMissingVerdictAttempts++;

                        if (RetryLimit.IsReached(verifierMissingVerdictAttempts, _options.MaxMissingSubmissionAttempts))
                        {
                            await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                            return Result.Fail<ReportWorkflowResult>("Report Verifier Agent finished without submitting a verdict.");
                        }

                        verifierMessages.Add(_messageBuilder.CreateMissingVerifierVerdictMessage());
                        continue;
                    }

                    if (verdict.Approved)
                    {
                        await _reportIssueStore.PromoteWorkingReportAsync(ruleReportKey, ruleFlowKey, cancellationToken).ConfigureAwait(false);
                        IReadOnlyList<ReportStoredIssue> repositoryIssues =
                            await _reportIssueStore.GetLatestSnapshotAsync(ruleReportKey, cancellationToken).ConfigureAwait(false);

                        return Result.Ok(ResultFactory.Create(
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

                    if (RetryLimit.IsReached(verifierRejectionAttempts, _options.MaxVerifierRejectionAttempts))
                    {
                        await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                        await _reportIssueStore.PromoteWorkingReportAsync(ruleReportKey, ruleFlowKey, cancellationToken).ConfigureAwait(false);
                        IReadOnlyList<ReportStoredIssue> repositoryIssues =
                            await _reportIssueStore.GetLatestSnapshotAsync(ruleReportKey, cancellationToken).ConfigureAwait(false);

                        return Result.Ok(ResultFactory.Create(
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
                    break;
                }
            }
        }
        finally
        {
            _verdictBuffer.Reset(reportVerdictScopeKey);
            await _reportIssueStore.ClearWorkingReportAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Captures restorable state for one report workflow attempt.
    /// </summary>
    /// <param name="attemptId">Attempt identifier associated with the snapshot.</param>
    /// <returns>A lease pair that can restore both the report issue store and verdict buffer.</returns>
    private WorkflowAttemptLeasePair PrepareAttempt(Guid attemptId)
    {
        return new WorkflowAttemptLeasePair(
            _reportIssueStore.BeginAttempt(_ruleFlowKey, attemptId),
            _verdictBuffer.BeginAttempt(_reportVerdictScopeKey, attemptId));
    }
}
