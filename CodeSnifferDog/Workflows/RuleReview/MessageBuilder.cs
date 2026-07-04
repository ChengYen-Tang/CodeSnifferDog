using CodeSnifferDog.Json;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Workflows.Common;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.RuleReview;

/// <summary>
/// Builds the chat messages used by the rule-review workflow and its verifier loop.
/// </summary>
/// <param name="messageTemplates">Prompt-backed text fragments used to compose workflow messages.</param>
internal sealed class MessageBuilder(MessageTemplates messageTemplates)
{
    private readonly MessageTemplates _messageTemplates = messageTemplates;

    /// <summary>
    /// Creates the initial rule-review conversation.
    /// </summary>
    /// <returns>The initial review conversation messages.</returns>
    public List<ChatMessage> CreateReviewMessages()
        =>
    [
        new(ChatRole.User, _messageTemplates.RuleReviewStartMessage),
    ];

    /// <summary>
    /// Creates the retry message used when the reviewer finishes without submitting issues or a no-issue conclusion.
    /// </summary>
    /// <returns>The missing-submission retry message.</returns>
    public ChatMessage CreateMissingSubmissionMessage()
        =>
        new(ChatRole.User, _messageTemplates.MissingRuleReviewSubmissionMessage);

    /// <summary>
    /// Creates verifier messages from the rule-review output.
    /// </summary>
    /// <param name="issues">Issues submitted by the reviewer.</param>
    /// <param name="noIssueConclusion">No-issue conclusion submitted by the reviewer when no issues were found.</param>
    /// <returns>The verifier conversation messages.</returns>
    public List<ChatMessage> CreateVerifierMessages(
        IReadOnlyList<StoredIssue> issues,
        NoIssueConclusion? noIssueConclusion)
        =>
    [
        new(ChatRole.User, BuildVerifierInput(issues, noIssueConclusion)),
    ];

    /// <summary>
    /// Creates the retry message used when the verifier finishes without publishing a verdict.
    /// </summary>
    /// <returns>The missing-verdict retry message.</returns>
    public ChatMessage CreateMissingVerifierVerdictMessage()
        =>
        new(ChatRole.User, WorkflowRetryMessages.MissingVerifierVerdictMessage);

    /// <summary>
    /// Builds the verifier payload from either the issue list or the no-issue conclusion.
    /// </summary>
    /// <param name="issues">Issues submitted by the reviewer.</param>
    /// <param name="noIssueConclusion">No-issue conclusion submitted by the reviewer when no issues were found.</param>
    /// <returns>The formatted verifier input.</returns>
    /// <exception cref="InvalidOperationException">Neither issues nor a no-issue conclusion were provided.</exception>
    private string BuildVerifierInput(
        IReadOnlyList<StoredIssue> issues,
        NoIssueConclusion? noIssueConclusion)
    {
        string payload = issues.Count > 0
            ? CodeSnifferDogJson.Serialize(issues)
            : CodeSnifferDogJson.Serialize(noIssueConclusion ?? throw new InvalidOperationException("A review result is required for verification."));

        return $"{_messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{payload}";
    }
}
