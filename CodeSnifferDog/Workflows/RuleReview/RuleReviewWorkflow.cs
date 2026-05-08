using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.RuleReview;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CodeSnifferDog.Workflows.RuleReview;

public sealed class RuleReviewWorkflow(
    Func<string, string, string, StoredProjectPlanTaskItem, IAgentEventScope, AIAgent> ruleReviewAgentFactory,
    Func<string, string, string, StoredProjectPlanTaskItem, IAgentEventScope, AIAgent> reviewVerifierAgentFactory,
    IRuleReviewIssueStore issueStore,
    ReviewVerdictBuffer verdictBuffer,
    PromptAssetReader? promptAssetReader = null,
    RuleReviewWorkflowOptions? options = null,
    IAgentEventBus? agentEventBus = null)
{
    private readonly Func<string, string, string, StoredProjectPlanTaskItem, IAgentEventScope, AIAgent> _ruleReviewAgentFactory = ruleReviewAgentFactory;
    private readonly Func<string, string, string, StoredProjectPlanTaskItem, IAgentEventScope, AIAgent> _reviewVerifierAgentFactory = reviewVerifierAgentFactory;
    private readonly IRuleReviewIssueStore _issueStore = issueStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;
    private readonly RuleReviewWorkflowMessageTemplates _messageTemplates =
        new(promptAssetReader ?? new PromptAssetReader());
    private readonly RuleReviewWorkflowOptions _options = options ?? new();
    private readonly IAgentEventBus _agentEventBus = agentEventBus ?? NoOpAgentEventBus.Instance;

    public async Task<Result<RuleReviewWorkflowResult>> RunAsync(
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
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
        await _issueStore.ClearAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);
        _verdictBuffer.Reset(reviewVerdictScopeKey);

        try
        {
            string groupKey = AgentStatusCatalog.CreateReviewTaskGroupKey(taskItem);
            IAgentEventScope reviewAgentScope = _agentEventBus.CreateScope(groupKey, AgentStatusCatalog.CreateRuleReviewAgentKey(taskItem, ruleKey));
            IAgentEventScope verifierAgentScope = _agentEventBus.CreateScope(groupKey, AgentStatusCatalog.CreateReviewVerifierAgentKey(taskItem, ruleKey));

            Result<AIAgent> createRuleReviewAgentResult = TryCreateAgent(
                () => _ruleReviewAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, reviewAgentScope),
                "Rule Review Agent");

            if (createRuleReviewAgentResult.IsFailed)
                return createRuleReviewAgentResult.ToResult<RuleReviewWorkflowResult>();
            await reviewAgentScope.PublishCreatedAsync(
                AgentStatusCatalog.CreateRuleReviewAgentDisplayName(ruleKey),
                AgentStatusCatalog.WaitingStatus,
                cancellationToken).ConfigureAwait(false);

            Result<AIAgent> createReviewVerifierAgentResult = TryCreateAgent(
                () => _reviewVerifierAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, verifierAgentScope),
                "Review Verifier Agent");

            if (createReviewVerifierAgentResult.IsFailed)
                return createReviewVerifierAgentResult.ToResult<RuleReviewWorkflowResult>();
            await verifierAgentScope.PublishCreatedAsync(
                AgentStatusCatalog.CreateReviewVerifierAgentDisplayName(ruleKey),
                AgentStatusCatalog.WaitingStatus,
                cancellationToken).ConfigureAwait(false);

            AIAgent ruleReviewAgent = createRuleReviewAgentResult.Value;
            AIAgent reviewVerifierAgent = createReviewVerifierAgentResult.Value;
            List<ChatMessage> reviewMessages = CreateReviewMessages();
            int reviewPublishedMessageCount = 0;

            int reviewAttempts = 0;
            int verifierAttempts = 0;
            int verifierRejectionAttempts = 0;
            int missingSubmissionAttempts = 0;
            int ruleReviewAgentResetCount = 0;

            while (true)
            {
                reviewAttempts++;
                await reviewAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.RunningStatus, cancellationToken).ConfigureAwait(false);

                (Result runReviewResult, reviewPublishedMessageCount) = await RunAgentAsync(
                    ruleReviewAgent,
                    reviewMessages,
                    reviewAgentScope,
                    reviewPublishedMessageCount,
                    cancellationToken).ConfigureAwait(false);

                if (runReviewResult.IsFailed)
                {
                    await reviewAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                    return runReviewResult.ToResult<RuleReviewWorkflowResult>();
                }

                await reviewAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.CompletedStatus, cancellationToken).ConfigureAwait(false);

                IReadOnlyList<StoredRuleReviewIssue> issues = await _issueStore.ListAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);
                NoIssueConclusion? noIssueConclusion = await _issueStore.GetNoIssueConclusionAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);

                if (issues.Count == 0 && noIssueConclusion is null)
                {
                    missingSubmissionAttempts++;

                    if (missingSubmissionAttempts >= _options.MaxMissingSubmissionAttempts)
                    {
                        ruleReviewAgentResetCount++;

                        if (ruleReviewAgentResetCount > _options.MaxRuleReviewAgentResets)
                        {
                            await reviewAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                            ReviewVerdict missingSubmissionVerdict = new()
                            {
                                Approved = false,
                                Message = "Rule Review Agent did not submit any issues or a no-issue conclusion after the allowed reset limit.",
                            };

                            return Result.Ok(CreateResult(
                                taskItem,
                                ruleKey,
                                ruleMarkdown,
                                issues,
                                noIssueConclusion,
                                missingSubmissionVerdict,
                                reviewAttempts,
                                verifierAttempts,
                                ruleReviewAgentResetCount,
                                continuedAfterVerifierRejectionLimit: false,
                                stoppedAfterMissingSubmissionLimit: true));
                        }

                        Result<AIAgent> recreateRuleReviewAgentResult = TryCreateAgent(
                            () => _ruleReviewAgentFactory(repositoryRootPath, ruleKey, ruleMarkdown, taskItem, reviewAgentScope),
                            "Rule Review Agent");

                        if (recreateRuleReviewAgentResult.IsFailed)
                            return recreateRuleReviewAgentResult.ToResult<RuleReviewWorkflowResult>();

                        ruleReviewAgent = recreateRuleReviewAgentResult.Value;
                        await reviewAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.WaitingStatus, cancellationToken).ConfigureAwait(false);
                        reviewMessages = CreateReviewMessages();
                        reviewPublishedMessageCount = 0;
                        missingSubmissionAttempts = 0;
                        continue;
                    }

                    reviewMessages.Add(new ChatMessage(ChatRole.User, _messageTemplates.MissingRuleReviewSubmissionMessage));
                    continue;
                }

                missingSubmissionAttempts = 0;
                verifierAttempts++;
                _verdictBuffer.Reset(reviewVerdictScopeKey);
                await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.RunningStatus, cancellationToken).ConfigureAwait(false);

                List<ChatMessage> verifierMessages =
                [
                    new(ChatRole.User, BuildVerifierInput(issues, noIssueConclusion)),
                ];
                int verifierPublishedMessageCount = 0;

                (Result runVerifierResult, verifierPublishedMessageCount) = await RunAgentAsync(
                    reviewVerifierAgent,
                    verifierMessages,
                    verifierAgentScope,
                    verifierPublishedMessageCount,
                    cancellationToken).ConfigureAwait(false);

                if (runVerifierResult.IsFailed)
                {
                    await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                    return runVerifierResult.ToResult<RuleReviewWorkflowResult>();
                }

                await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.CompletedStatus, cancellationToken).ConfigureAwait(false);

                if (_verdictBuffer.GetLatest(reviewVerdictScopeKey) is not ReviewVerdict verdict)
                {
                    await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                    return Result.Fail<RuleReviewWorkflowResult>("Review Verifier Agent finished without submitting a verdict.");
                }

                if (verdict.Approved)
                {
                    return Result.Ok(CreateResult(
                        taskItem,
                        ruleKey,
                        ruleMarkdown,
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

                if (verifierRejectionAttempts >= _options.MaxVerifierRejectionAttempts)
                {
                    await verifierAgentScope.PublishStatusChangedAsync(AgentStatusCatalog.DegradedStatus, cancellationToken).ConfigureAwait(false);
                    return Result.Ok(CreateResult(
                        taskItem,
                        ruleKey,
                        ruleMarkdown,
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
            }
        }
        finally
        {
            _verdictBuffer.Reset(reviewVerdictScopeKey);
            await _issueStore.ClearAsync(ruleFlowKey, cancellationToken).ConfigureAwait(false);
        }
    }

    private static RuleReviewWorkflowResult CreateResult(
        StoredProjectPlanTaskItem taskItem,
        string ruleKey,
        string _,
        IReadOnlyList<StoredRuleReviewIssue> issues,
        NoIssueConclusion? noIssueConclusion,
        ReviewVerdict verdict,
        int reviewAttempts,
        int verifierAttempts,
        int ruleReviewAgentResetCount,
        bool continuedAfterVerifierRejectionLimit,
        bool stoppedAfterMissingSubmissionLimit) =>
        new()
        {
            TaskItem = taskItem,
            RuleKey = ruleKey,
            Issues = issues,
            NoIssueConclusion = noIssueConclusion,
            Verdict = verdict,
            ContinuedAfterVerifierRejectionLimit = continuedAfterVerifierRejectionLimit,
            StoppedAfterMissingSubmissionLimit = stoppedAfterMissingSubmissionLimit,
            ReviewAttempts = reviewAttempts,
            VerifierAttempts = verifierAttempts,
            RuleReviewAgentResetCount = ruleReviewAgentResetCount,
        };

    private static async Task<(Result Result, int PublishedMessageCount)> RunAgentAsync(
        AIAgent agent,
        List<ChatMessage> messages,
        IAgentEventScope eventScope,
        int publishedMessageCount,
        CancellationToken cancellationToken)
    {
        try
        {
            await PublishPendingUserMessagesAsync(messages, eventScope, publishedMessageCount, cancellationToken).ConfigureAwait(false);
            AgentResponse response = await agent.RunAsync(messages, session: null, options: null, cancellationToken).ConfigureAwait(false);

            foreach (ChatMessage message in response.Messages)
            {
                messages.Add(message);
                if (message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text))
                    await eventScope.PublishAssistantMessageAsync(message.Text, cancellationToken).ConfigureAwait(false);
            }

            publishedMessageCount = messages.Count;

            return (Result.Ok(), publishedMessageCount);
        }
        catch (Exception ex)
        {
            return (Result.Fail(new ExceptionalError($"Agent run failed: {ex}", ex)), publishedMessageCount);
        }
    }

    private static async ValueTask PublishPendingUserMessagesAsync(
        List<ChatMessage> messages,
        IAgentEventScope eventScope,
        int publishedMessageCount,
        CancellationToken cancellationToken)
    {
        for (int index = publishedMessageCount; index < messages.Count; index++)
        {
            ChatMessage message = messages[index];
            if (message.Role == ChatRole.User && !string.IsNullOrWhiteSpace(message.Text))
                await eventScope.PublishUserMessageAsync(message.Text, cancellationToken).ConfigureAwait(false);
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

    private List<ChatMessage> CreateReviewMessages()
        =>
    [
        new(ChatRole.User, _messageTemplates.RuleReviewStartMessage),
    ];

    private string BuildVerifierInput(
        IReadOnlyList<StoredRuleReviewIssue> issues,
        NoIssueConclusion? noIssueConclusion)
    {
        string payload = issues.Count > 0
            ? JsonSerializer.Serialize(issues)
            : JsonSerializer.Serialize(noIssueConclusion ?? throw new InvalidOperationException("A review result is required for verification."));

        return $"{_messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{payload}";
    }
}
