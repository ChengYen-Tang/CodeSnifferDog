using CodeSnifferDog.Json;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Workflows.Common;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.Scan;

/// <summary>
/// Builds the chat messages used by the scan workflow and its verifier loop.
/// </summary>
/// <param name="messageTemplates">Prompt-backed text fragments used to compose workflow messages.</param>
internal sealed class MessageBuilder(MessageTemplates messageTemplates)
{
    private readonly MessageTemplates _messageTemplates = messageTemplates;

    /// <summary>
    /// Creates the initial scan prompt for a repository root path.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path that the scan agent should analyze.</param>
    /// <returns>The initial scan conversation messages.</returns>
    public List<ChatMessage> CreateScanMessages(string repositoryRootPath)
        =>
    [
        new(ChatRole.User, BuildScanInput(repositoryRootPath)),
    ];

    /// <summary>
    /// Creates the retry message used when the scan agent finishes without submitting projects.
    /// </summary>
    /// <returns>The missing-submission retry message.</returns>
    public ChatMessage CreateMissingSubmissionMessage()
        =>
        new(ChatRole.User, _messageTemplates.MissingScanSubmissionMessage);

    /// <summary>
    /// Creates verifier messages for the projects returned by the scan agent.
    /// </summary>
    /// <param name="projects">Projects submitted by the scan agent.</param>
    /// <returns>The verifier conversation messages.</returns>
    public List<ChatMessage> CreateVerifierMessages(IReadOnlyList<StoredScanProject> projects)
        =>
    [
        new(ChatRole.User, BuildVerifierInput(projects)),
    ];

    /// <summary>
    /// Creates the retry message used when the verifier finishes without publishing a verdict.
    /// </summary>
    /// <returns>The missing-verdict retry message.</returns>
    public ChatMessage CreateMissingVerifierVerdictMessage()
        =>
        new(ChatRole.User, WorkflowRetryMessages.MissingVerifierVerdictMessage);

    /// <summary>
    /// Builds the scan prompt payload from the repository root path.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path that the scan agent should analyze.</param>
    /// <returns>The formatted scan input.</returns>
    private string BuildScanInput(string repositoryRootPath)
        =>
        $"{_messageTemplates.ScanInputPrefix}{Environment.NewLine}{Environment.NewLine}{repositoryRootPath}";

    /// <summary>
    /// Builds the verifier payload from serialized scan projects.
    /// </summary>
    /// <param name="projects">Projects submitted by the scan agent.</param>
    /// <returns>The formatted verifier input.</returns>
    private string BuildVerifierInput(IReadOnlyList<StoredScanProject> projects)
        =>
        $"{_messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(projects)}";
}
