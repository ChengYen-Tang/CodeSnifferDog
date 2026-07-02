using CodeSnifferDog.Json;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.Report;

internal sealed class MessageBuilder(MessageTemplates messageTemplates)
{
    private readonly MessageTemplates _messageTemplates = messageTemplates;

    public List<ChatMessage> CreateAggregatorMessages(IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues)
        =>
    [
        new(ChatRole.User, BuildAggregatorInput(currentFlowIssues)),
    ];

    public List<ChatMessage> CreateVerifierMessages(RuleReportDiff diff)
        =>
    [
        new(ChatRole.User, BuildVerifierInput(diff)),
    ];

    private string BuildAggregatorInput(IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues)
        =>
        $"{_messageTemplates.AggregatorInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(currentFlowIssues)}";

    private string BuildVerifierInput(RuleReportDiff diff)
        =>
        $"{_messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(diff)}";
}
