using System.Text.Json;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.Report;

public sealed class RuleReportWorkflow(
    Func<string, string, StoredProjectPlanTaskItem, AIAgent> reportAggregatorAgentFactory,
    Func<string, string, StoredProjectPlanTaskItem, IReadOnlyList<StoredRuleReviewIssue>, AIAgent> reportVerifierAgentFactory,
    IRuleReportIssueStore reportIssueStore,
    ReviewVerdictBuffer verdictBuffer,
    PromptAssetReader? promptAssetReader = null,
    RuleReportWorkflowOptions? options = null)
{
    private readonly Func<string, string, StoredProjectPlanTaskItem, AIAgent> _reportAggregatorAgentFactory = reportAggregatorAgentFactory;
    private readonly Func<string, string, StoredProjectPlanTaskItem, IReadOnlyList<StoredRuleReviewIssue>, AIAgent> _reportVerifierAgentFactory = reportVerifierAgentFactory;
    private readonly IRuleReportIssueStore _reportIssueStore = reportIssueStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly RuleReportWorkflowMessageTemplates _messageTemplates =
        new(promptAssetReader ?? new PromptAssetReader());
    private readonly RuleReportWorkflowOptions _options = options ?? new();

    public async Task<Result<RuleReportWorkflowResult>> RunAsync(
        string repositoryRootPath,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRootPath))
            return Result.Fail<RuleReportWorkflowResult>("Repository root path is required.");

        if (string.IsNullOrWhiteSpace(ruleMarkdown))
            return Result.Fail<RuleReportWorkflowResult>("Rule markdown is required.");

        ArgumentNullException.ThrowIfNull(taskItem);
        ArgumentNullException.ThrowIfNull(currentFlowIssues);

        if (currentFlowIssues.Count == 0)
            return Result.Fail<RuleReportWorkflowResult>("Current flow issues are required for report aggregation.");

        repositoryRootPath = repositoryRootPath.Trim();
        ruleMarkdown = ruleMarkdown.Trim();
        RuleFlowKey ruleFlowKey =
            RuleScopeKeyFactory.CreateRuleFlowKey(repositoryRootPath, taskItem.ProjectPlanTaskItemId, ruleMarkdown);
        RuleReportKey ruleReportKey = RuleScopeKeyFactory.CreateRuleReportKey(repositoryRootPath, ruleMarkdown);
        string reportVerdictScopeKey = RuleScopeKeyFactory.CreateReportVerdictScopeKey(ruleFlowKey);

        Result<AIAgent> createAggregatorResult = TryCreateAgent(
            () => _reportAggregatorAgentFactory(repositoryRootPath, ruleMarkdown, taskItem),
            "Report Aggregator Agent");

        if (createAggregatorResult.IsFailed)
            return createAggregatorResult.ToResult<RuleReportWorkflowResult>();

        Result<AIAgent> createVerifierResult = TryCreateAgent(
            () => _reportVerifierAgentFactory(repositoryRootPath, ruleMarkdown, taskItem, currentFlowIssues),
            "Report Verifier Agent");

        if (createVerifierResult.IsFailed)
            return createVerifierResult.ToResult<RuleReportWorkflowResult>();

        AIAgent reportAggregatorAgent = createAggregatorResult.Value;
        AIAgent reportVerifierAgent = createVerifierResult.Value;
        List<ChatMessage> aggregatorMessages = CreateAggregatorMessages(currentFlowIssues);
        await _reportIssueStore.InitializeWorkingReportAsync(ruleReportKey, ruleFlowKey, cancellationToken).ConfigureAwait(false);

        int aggregatorAttempts = 0;
        int verifierAttempts = 0;
        int verifierRejectionAttempts = 0;

        while (true)
        {
            aggregatorAttempts++;

            Result runAggregatorResult = await RunAgentAsync(reportAggregatorAgent, aggregatorMessages, cancellationToken).ConfigureAwait(false);

            if (runAggregatorResult.IsFailed)
                return runAggregatorResult.ToResult<RuleReportWorkflowResult>();

            RuleReportDiff diff = await ComputeAndStoreDiffAsync(ruleReportKey, ruleFlowKey, cancellationToken).ConfigureAwait(false);

            verifierAttempts++;
            _verdictBuffer.Reset(reportVerdictScopeKey);

            List<ChatMessage> verifierMessages =
            [
                new(ChatRole.User, BuildVerifierInput(diff)),
            ];

            Result runVerifierResult = await RunAgentAsync(reportVerifierAgent, verifierMessages, cancellationToken).ConfigureAwait(false);

            if (runVerifierResult.IsFailed)
                return runVerifierResult.ToResult<RuleReportWorkflowResult>();

            if (_verdictBuffer.GetLatest(reportVerdictScopeKey) is not ReviewVerdict verdict)
                return Result.Fail<RuleReportWorkflowResult>("Report Verifier Agent finished without submitting a verdict.");

            if (verdict.Approved)
            {
                await _reportIssueStore.PromoteWorkingReportAsync(ruleReportKey, ruleFlowKey, cancellationToken).ConfigureAwait(false);
                IReadOnlyList<StoredRuleReportIssue> repositoryIssues =
                    await _reportIssueStore.GetLatestSnapshotAsync(ruleReportKey, cancellationToken).ConfigureAwait(false);
                await _reportIssueStore.ClearWorkingReportAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);

                return Result.Ok(new RuleReportWorkflowResult
                {
                    TaskItem = taskItem,
                    RuleMarkdown = ruleMarkdown,
                    CurrentFlowIssues = currentFlowIssues,
                    Diff = diff,
                    RepositoryIssues = repositoryIssues,
                    Verdict = verdict,
                    ReportVerifierApproved = true,
                    ContinuedAfterVerifierRejectionLimit = false,
                    AggregatorAttempts = aggregatorAttempts,
                    VerifierAttempts = verifierAttempts,
                });
            }

            verifierRejectionAttempts++;

            if (verifierRejectionAttempts >= _options.MaxVerifierRejectionAttempts)
            {
                await _reportIssueStore.PromoteWorkingReportAsync(ruleReportKey, ruleFlowKey, cancellationToken).ConfigureAwait(false);
                IReadOnlyList<StoredRuleReportIssue> repositoryIssues =
                    await _reportIssueStore.GetLatestSnapshotAsync(ruleReportKey, cancellationToken).ConfigureAwait(false);
                await _reportIssueStore.ClearWorkingReportAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);

                return Result.Ok(new RuleReportWorkflowResult
                {
                    TaskItem = taskItem,
                    RuleMarkdown = ruleMarkdown,
                    CurrentFlowIssues = currentFlowIssues,
                    Diff = diff,
                    RepositoryIssues = repositoryIssues,
                    Verdict = verdict,
                    ReportVerifierApproved = false,
                    ContinuedAfterVerifierRejectionLimit = true,
                    AggregatorAttempts = aggregatorAttempts,
                    VerifierAttempts = verifierAttempts,
                });
            }

            aggregatorMessages.Add(new ChatMessage(ChatRole.User, verdict.Message));
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
        left.FileOrFunction == right.FileOrFunction &&
        left.RelevantCodePatternOrExpression == right.RelevantCodePatternOrExpression &&
        left.WhyThisIsAProblem == right.WhyThisIsAProblem &&
        left.Confidence == right.Confidence &&
        left.FollowUpFiles == right.FollowUpFiles &&
        left.SuggestedFixDirection == right.SuggestedFixDirection &&
        left.ReviewStrategy == right.ReviewStrategy &&
        left.ScopeCoverage == right.ScopeCoverage &&
        left.CrossScopeAnalysis == right.CrossScopeAnalysis;

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

    private static Result<AIAgent> TryCreateAgent(Func<AIAgent> factory, string agentName)
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
        $"{_messageTemplates.AggregatorInputPrefix}{Environment.NewLine}{Environment.NewLine}{JsonSerializer.Serialize(currentFlowIssues)}";

    private string BuildVerifierInput(RuleReportDiff diff)
        =>
        $"{_messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{JsonSerializer.Serialize(diff)}";
}
