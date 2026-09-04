using CodeSnifferDog.Json;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools.Listing;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Workflows.Common;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.ProjectPlan;

/// <summary>
/// Builds the chat messages used by the project-plan workflow and its verifier loop.
/// </summary>
/// <param name="messageTemplates">Prompt-backed text fragments used to compose workflow messages.</param>
internal sealed class MessageBuilder(MessageTemplates messageTemplates)
{
    private readonly MessageTemplates _messageTemplates = messageTemplates;

    /// <summary>
    /// Creates the initial planning prompt for one scanned project.
    /// </summary>
    /// <param name="scanProject">Scanned project that the planner should decompose into task items.</param>
    /// <returns>The initial planner conversation messages.</returns>
    public List<ChatMessage> CreatePlanMessages(StoredScanProject scanProject)
        =>
    [
        new(ChatRole.User, BuildPlanInput(scanProject)),
    ];

    /// <summary>
    /// Creates the retry message used when the planner finishes without submitting task items.
    /// </summary>
    /// <returns>The missing-submission retry message.</returns>
    public ChatMessage CreateMissingSubmissionMessage()
        =>
        new(ChatRole.User, _messageTemplates.MissingProjectPlanSubmissionMessage);

    /// <summary>
    /// Creates verifier messages for the first bounded page of task items returned by the planner.
    /// </summary>
    /// <param name="taskItemPage">Bounded task item indexes submitted by the planner.</param>
    /// <returns>The verifier conversation messages.</returns>
    public List<ChatMessage> CreateVerifierMessages(TaskItemPage taskItemPage)
        =>
    [
        new(ChatRole.User, BuildVerifierInput(taskItemPage)),
    ];

    /// <summary>
    /// Creates the retry message used when the verifier finishes without publishing a verdict.
    /// </summary>
    /// <returns>The missing-verdict retry message.</returns>
    public ChatMessage CreateMissingVerifierVerdictMessage()
        =>
        new(ChatRole.User, WorkflowRetryMessages.MissingVerifierVerdictMessage);

    /// <summary>
    /// Builds the planning prompt payload from one scanned project.
    /// </summary>
    /// <param name="scanProject">Scanned project that the planner should decompose into task items.</param>
    /// <returns>The formatted planner input.</returns>
    private string BuildPlanInput(StoredScanProject scanProject)
        =>
        $"{_messageTemplates.PlanInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(scanProject)}";

    /// <summary>
    /// Builds the verifier payload from serialized bounded task item indexes.
    /// </summary>
    /// <param name="taskItemPage">Bounded task item indexes submitted by the planner.</param>
    /// <returns>The formatted verifier input.</returns>
    private string BuildVerifierInput(TaskItemPage taskItemPage)
        =>
        $"{_messageTemplates.VerifierInputPrefix}{Environment.NewLine}{Environment.NewLine}{CodeSnifferDogJson.Serialize(taskItemPage)}";
}
