using CodeSnifferDog.Json;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Workflows.Common;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.ProjectPlan;

internal sealed class MessageBuilder(MessageTemplates messageTemplates)
{
    private readonly MessageTemplates _messageTemplates = messageTemplates;

    public List<ChatMessage> CreatePlanMessages(StoredScanProject scanProject)
        =>
    [
        new(ChatRole.User, BuildPlanInput(scanProject)),
    ];

    public ChatMessage CreateMissingSubmissionMessage()
        =>
        new(ChatRole.User, _messageTemplates.MissingProjectPlanSubmissionMessage);

    public List<ChatMessage> CreateVerifierMessages(IReadOnlyList<StoredTaskItem> taskItems)
        =>
    [
        new(ChatRole.User, BuildVerifierInput(taskItems)),
    ];

    public ChatMessage CreateMissingVerifierVerdictMessage()
        =>
        new(ChatRole.User, WorkflowRetryMessages.MissingVerifierVerdictMessage);

    private string BuildPlanInput(StoredScanProject scanProject)
        =>
        $"{_messageTemplates.PlanInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(scanProject)}";

    private string BuildVerifierInput(IReadOnlyList<StoredTaskItem> taskItems)
        =>
        $"{_messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(taskItems)}";
}
