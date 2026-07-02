using CodeSnifferDog.Json;
using CodeSnifferDog.Models.RuleReview;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.RuleReview;

internal sealed class MessageBuilder(MessageTemplates messageTemplates)
{
    private readonly MessageTemplates _messageTemplates = messageTemplates;

    public List<ChatMessage> CreateReviewMessages()
        =>
    [
        new(ChatRole.User, _messageTemplates.RuleReviewStartMessage),
    ];

    public ChatMessage CreateMissingSubmissionMessage()
        =>
        new(ChatRole.User, _messageTemplates.MissingRuleReviewSubmissionMessage);

    public List<ChatMessage> CreateVerifierMessages(
        IReadOnlyList<StoredRuleReviewIssue> issues,
        NoIssueConclusion? noIssueConclusion)
        =>
    [
        new(ChatRole.User, BuildVerifierInput(issues, noIssueConclusion)),
    ];

    private string BuildVerifierInput(
        IReadOnlyList<StoredRuleReviewIssue> issues,
        NoIssueConclusion? noIssueConclusion)
    {
        string payload = issues.Count > 0
            ? CodeSnifferDogJson.Serialize(issues)
            : CodeSnifferDogJson.Serialize(noIssueConclusion ?? throw new InvalidOperationException("A review result is required for verification."));

        return $"{_messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{payload}";
    }
}
