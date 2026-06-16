using CodeSnifferDog.Json;
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
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly RuleReportWorkflowMessageTemplates _messageTemplates =
        new(promptAssetReader ?? new PromptAssetReader());
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

            Result<AgentCreationResult> createAggregatorResult = TryCreateAgent(
                () => _reportAggregatorAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, aggregatorAgentScope),
                "Report Aggregator Agent");

            if (createAggregatorResult.IsFailed)
                return createAggregatorResult.ToResult<RuleReportWorkflowResult>();
            await aggregatorAgentScope.PublishCreatedAsync(
                AgentStatusCatalog.CreateReportAggregatorAgentDisplayName(ruleKey),
                createAggregatorResult.Value.SystemPrompt,
                AgentStatusCatalog.WaitingStatus,
                cancellationToken).ConfigureAwait(false);

            Result<AgentCreationResult> createVerifierResult = TryCreateAgent(
                () => _reportVerifierAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, currentFlowIssues, verifierAgentScope),
                "Report Verifier Agent");

            if (createVerifierResult.IsFailed)
                return createVerifierResult.ToResult<RuleReportWorkflowResult>();
            await verifierAgentScope.PublishCreatedAsync(
                AgentStatusCatalog.CreateReportVerifierAgentDisplayName(ruleKey),
                createVerifierResult.Value.SystemPrompt,
                AgentStatusCatalog.WaitingStatus,
                cancellationToken).ConfigureAwait(false);

            AIAgent reportAggregatorAgent = createAggregatorResult.Value.Agent;
            AIAgent reportVerifierAgent = createVerifierResult.Value.Agent;
            List<ChatMessage> aggregatorMessages = CreateAggregatorMessages(currentFlowIssues);
            int aggregatorPublishedMessageCount = 0;

            int aggregatorAttempts = 0;
            int verifierAttempts = 0;
            int verifierRejectionAttempts = 0;

            while (true)
            {
                aggregatorAttempts++;
                await aggregatorAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.RunningStatus, cancellationToken).ConfigureAwait(false);

                (Result runAggregatorResult, aggregatorPublishedMessageCount, reportAggregatorAgent) = await RunAgentAsync(
                    reportAggregatorAgent,
                    () => _reportAggregatorAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, aggregatorAgentScope).Agent,
                    aggregatorMessages,
                    aggregatorAgentScope,
                    aggregatorPublishedMessageCount,
                    cancellationToken).ConfigureAwait(false);

                if (runAggregatorResult.IsFailed)
                {
                    await aggregatorAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                    return runAggregatorResult.ToResult<RuleReportWorkflowResult>();
                }

                await aggregatorAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.CompletedStatus, cancellationToken).ConfigureAwait(false);

                RuleReportDiff diff = await ComputeAndStoreDiffAsync(ruleReportKey, ruleFlowKey, cancellationToken).ConfigureAwait(false);

                verifierAttempts++;
                _verdictBuffer.Reset(reportVerdictScopeKey);
                await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.RunningStatus, cancellationToken).ConfigureAwait(false);

                List<ChatMessage> verifierMessages =
                [
                    new(ChatRole.User, BuildVerifierInput(diff)),
                ];
                int verifierPublishedMessageCount = 0;

                (Result runVerifierResult, verifierPublishedMessageCount, reportVerifierAgent) = await RunAgentAsync(
                    reportVerifierAgent,
                    () => _reportVerifierAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, currentFlowIssues, verifierAgentScope).Agent,
                    verifierMessages,
                    verifierAgentScope,
                    verifierPublishedMessageCount,
                    cancellationToken).ConfigureAwait(false);

                if (runVerifierResult.IsFailed)
                {
                    await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                    return runVerifierResult.ToResult<RuleReportWorkflowResult>();
                }

                await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.CompletedStatus, cancellationToken).ConfigureAwait(false);

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

                    return Result.Ok(new RuleReportWorkflowResult
                    {
                        RuleKey = ruleKey,
                        TaskItem = taskItem,
                        Diff = diff,
                        RepositoryIssues = repositoryIssues,
                        Verdict = verdict,
                        ContinuedAfterVerifierRejectionLimit = false,
                        AggregatorAttempts = aggregatorAttempts,
                        VerifierAttempts = verifierAttempts,
                    });
                }

                verifierRejectionAttempts++;

                if (verifierRejectionAttempts >= _options.MaxVerifierRejectionAttempts)
                {
                    await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                    await _reportIssueStore.PromoteWorkingReportAsync(ruleReportKey, ruleFlowKey, cancellationToken).ConfigureAwait(false);
                    IReadOnlyList<StoredRuleReportIssue> repositoryIssues =
                        await _reportIssueStore.GetLatestSnapshotAsync(ruleReportKey, cancellationToken).ConfigureAwait(false);

                    return Result.Ok(new RuleReportWorkflowResult
                    {
                        RuleKey = ruleKey,
                        TaskItem = taskItem,
                        Diff = diff,
                        RepositoryIssues = repositoryIssues,
                        Verdict = verdict,
                        ContinuedAfterVerifierRejectionLimit = true,
                        AggregatorAttempts = aggregatorAttempts,
                        VerifierAttempts = verifierAttempts,
                    });
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

    private async Task<RuleReportDiff> ComputeAndStoreDiffAsync(
        RuleReportKey ruleReportKey,
        RuleFlowKey ruleFlowKey,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<StoredRuleReportIssue> previousSnapshot = await _reportIssueStore.GetLatestSnapshotAsync(ruleReportKey, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<StoredRuleReportIssue> currentIssues = await _reportIssueStore.ListAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);
        RuleReportDiff diff = BuildDiff(previousSnapshot, currentIssues);
        await _reportIssueStore.SetLatestDiffAsync(ruleFlowKey, diff, cancellationToken).ConfigureAwait(false);
        return diff;
    }

    private static RuleReportDiff BuildDiff(
        IReadOnlyList<StoredRuleReportIssue> previousSnapshot,
        IReadOnlyList<StoredRuleReportIssue> currentIssues)
    {
        Dictionary<string, StoredRuleReportIssue> previousById = previousSnapshot.ToDictionary(issue => issue.RuleReportIssueId, StringComparer.Ordinal);
        Dictionary<string, StoredRuleReportIssue> currentById = currentIssues.ToDictionary(issue => issue.RuleReportIssueId, StringComparer.Ordinal);

        List<StoredRuleReportIssue> created = [];
        List<StoredRuleReportIssue> updated = [];
        List<StoredRuleReportIssue> deleted = [];

        foreach ((string id, StoredRuleReportIssue currentIssue) in currentById)
        {
            if (!previousById.TryGetValue(id, out StoredRuleReportIssue? previousIssue))
            {
                created.Add(currentIssue);
                continue;
            }

            if (!AreEquivalent(previousIssue, currentIssue))
                updated.Add(currentIssue);
        }

        foreach ((string id, StoredRuleReportIssue previousIssue) in previousById)
            if (!currentById.ContainsKey(id))
                deleted.Add(previousIssue);

        return new RuleReportDiff
        {
            CreatedIssues = created,
            UpdatedIssues = updated,
            DeletedIssues = deleted,
        };
    }

    private static bool AreEquivalent(StoredRuleReportIssue left, StoredRuleReportIssue right)
        =>
        left.RuleReportIssueId == right.RuleReportIssueId &&
        left.IssueType == right.IssueType &&
        left.Severity == right.Severity &&
        left.FileOrFunction == right.FileOrFunction &&
        left.RelevantCodePatternOrExpression == right.RelevantCodePatternOrExpression &&
        left.WhyThisIsAProblem == right.WhyThisIsAProblem &&
        left.Confidence == right.Confidence &&
        left.FollowUpFiles == right.FollowUpFiles &&
        left.SuggestedFixDirection == right.SuggestedFixDirection &&
        left.ReviewStrategy == right.ReviewStrategy &&
        left.ScopeCoverage == right.ScopeCoverage &&
        left.CrossScopeAnalysis == right.CrossScopeAnalysis;

    private Task<(Result Result, int PublishedMessageCount, AIAgent Agent)> RunAgentAsync(
        AIAgent agent,
        Func<AIAgent> agentFactory,
        List<ChatMessage> messages,
        IAgentEventScope eventScope,
        int publishedMessageCount,
        CancellationToken cancellationToken) =>
        AgentRunGuard.RunAsync(
            agent,
            agentFactory,
            PrepareAttempt,
            RestoreAttempt,
            messages,
            eventScope,
            publishedMessageCount,
            _options.AgentRunTimeout,
            _options.MaxConsecutiveRunFailures,
            cancellationToken);

    private AttemptState PrepareAttempt(Guid attemptId)
    {
        return new AttemptState(
            _reportIssueStore.BeginAttempt(_ruleFlowKey, attemptId),
            _verdictBuffer.BeginAttempt(_reportVerdictScopeKey, attemptId));
    }

    private void RestoreAttempt(AttemptState state)
    {
        state.StoreLease.Restore();
        state.VerdictLease.Restore();
    }

    private static Result<AgentCreationResult> TryCreateAgent(Func<AgentCreationResult> factory, string agentName)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        try
        {
            return Result.Ok(factory());
        }
        catch (Exception ex)
        {
            return Result.Fail(new ExceptionalError($"Failed to create {agentName}: {ex}", ex));
        }
    }

    private List<ChatMessage> CreateAggregatorMessages(IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues)
        =>
    [
        new(ChatRole.User, BuildAggregatorInput(currentFlowIssues)),
    ];

    private string BuildAggregatorInput(IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues)
        =>
        $"{_messageTemplates.AggregatorInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(currentFlowIssues)}";

    private string BuildVerifierInput(RuleReportDiff diff)
        =>
        $"{_messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(diff)}";

    private sealed class AttemptState(
        IAgentAttemptLease storeLease,
        IAgentAttemptLease verdictLease)
    {
        public IAgentAttemptLease StoreLease { get; } = storeLease;

        public IAgentAttemptLease VerdictLease { get; } = verdictLease;
    }
}
