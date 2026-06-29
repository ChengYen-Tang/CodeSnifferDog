using CodeSnifferDog.Json;
using CodeSnifferDog.Models.Scan;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.Scan;

internal sealed class ScanWorkflowMessageBuilder(ScanWorkflowMessageTemplates messageTemplates)
{
    private readonly ScanWorkflowMessageTemplates _messageTemplates = messageTemplates;

    public List<ChatMessage> CreateScanMessages(string repositoryRootPath)
        =>
    [
        new(ChatRole.User, BuildScanInput(repositoryRootPath)),
    ];

    public ChatMessage CreateMissingSubmissionMessage()
        =>
        new(ChatRole.User, _messageTemplates.MissingScanSubmissionMessage);

    public List<ChatMessage> CreateVerifierMessages(IReadOnlyList<StoredScanProject> projects)
        =>
    [
        new(ChatRole.User, BuildVerifierInput(projects)),
    ];

    private string BuildScanInput(string repositoryRootPath)
        =>
        $"{_messageTemplates.ScanInputPrefix}{Environment.NewLine}{Environment.NewLine}{repositoryRootPath}";

    private string BuildVerifierInput(IReadOnlyList<StoredScanProject> projects)
        =>
        $"{_messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(projects)}";
}
