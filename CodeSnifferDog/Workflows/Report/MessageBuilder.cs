using CodeSnifferDog.Json;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Workflows.Common;
using Microsoft.Extensions.AI;
using RuleReviewStoredIssue = CodeSnifferDog.Models.RuleReview.StoredIssue;

namespace CodeSnifferDog.Workflows.Report;

/// <summary>
/// Builds the chat messages used by the report workflow's aggregator and verifier agents.
/// </summary>
/// <param name="messageTemplates">Prompt-backed text fragments used to compose workflow messages.</param>
internal sealed class MessageBuilder(MessageTemplates messageTemplates)
{
    private readonly MessageTemplates _messageTemplates = messageTemplates;

    /// <summary>
    /// Creates aggregator messages from the current rule-review issues.
    /// </summary>
    /// <param name="currentFlowIssues">Issues produced by the current rule-review flow.</param>
    /// <returns>The aggregator conversation messages.</returns>
    public List<ChatMessage> CreateAggregatorMessages(IReadOnlyList<RuleReviewStoredIssue> currentFlowIssues)
        =>
    [
        new(ChatRole.User, BuildAggregatorInput(currentFlowIssues)),
    ];

    /// <summary>
    /// Creates verifier messages from one stored report diff.
    /// </summary>
    /// <param name="diff">Diff produced by the report aggregator.</param>
    /// <returns>The verifier conversation messages.</returns>
    public List<ChatMessage> CreateVerifierMessages(Diff diff)
        =>
    [
        new(ChatRole.User, BuildVerifierInput(diff)),
    ];

    /// <summary>
    /// Creates the retry message used when the verifier finishes without publishing a verdict.
    /// </summary>
    /// <returns>The missing-verdict retry message.</returns>
    public ChatMessage CreateMissingVerifierVerdictMessage()
        =>
        new(ChatRole.User, WorkflowRetryMessages.MissingVerifierVerdictMessage);

    /// <summary>
    /// Builds the aggregator payload from serialized current-flow issues.
    /// </summary>
    /// <param name="currentFlowIssues">Issues produced by the current rule-review flow.</param>
    /// <returns>The formatted aggregator input.</returns>
    private string BuildAggregatorInput(IReadOnlyList<RuleReviewStoredIssue> currentFlowIssues)
        =>
        $"{_messageTemplates.AggregatorInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(currentFlowIssues)}";

    /// <summary>
    /// Builds the verifier payload from one serialized diff.
    /// </summary>
    /// <param name="diff">Diff produced by the report aggregator.</param>
    /// <returns>The formatted verifier input.</returns>
    private string BuildVerifierInput(Diff diff)
        =>
        $"{_messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(diff)}";
}
