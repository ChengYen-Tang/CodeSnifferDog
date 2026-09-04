using CodeSnifferDog.Json;
using CodeSnifferDog.Models.ProjectPlan;
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
    /// Creates the initial rule-review conversation with its system-controlled task scope.
    /// </summary>
    /// <param name="taskItem">Task item that supplies the scope entry files.</param>
    /// <returns>The initial review conversation messages.</returns>
    public List<ChatMessage> CreateReviewMessages(StoredTaskItem taskItem)
        =>
    [
        new(ChatRole.User, BuildScopeInput(_messageTemplates.RuleReviewStartMessage, taskItem)),
    ];

    /// <summary>
    /// Creates the retry message used when the reviewer finishes without submitting issues or a no-issue conclusion.
    /// </summary>
    /// <returns>The missing-submission retry message.</returns>
    public ChatMessage CreateMissingSubmissionMessage()
        =>
        new(ChatRole.User, _messageTemplates.MissingRuleReviewSubmissionMessage);

    /// <summary>
    /// Creates verifier messages with the system-controlled task scope.
    /// </summary>
    /// <param name="taskItem">Task item that supplies the scope entry files.</param>
    /// <returns>The verifier conversation messages.</returns>
    public List<ChatMessage> CreateVerifierMessages(StoredTaskItem taskItem)
        =>
    [
        new(ChatRole.User, BuildScopeInput(_messageTemplates.VerifierInputPrefix, taskItem)),
    ];

    /// <summary>
    /// Creates the retry message used when the verifier finishes without publishing a verdict.
    /// </summary>
    /// <returns>The missing-verdict retry message.</returns>
    public ChatMessage CreateMissingVerifierVerdictMessage()
        =>
        new(ChatRole.User, WorkflowRetryMessages.MissingVerifierVerdictMessage);

    /// <summary>
    /// Builds a system-controlled task-scope payload for an agent input message.
    /// </summary>
    /// <param name="prefix">Fixed workflow instruction that introduces the payload.</param>
    /// <param name="taskItem">Task item that supplies the scope entry files.</param>
    /// <returns>The formatted user input.</returns>
    private static string BuildScopeInput(string prefix, StoredTaskItem taskItem)
    {
        ArgumentNullException.ThrowIfNull(taskItem);

        return $"{prefix}{Environment.NewLine}{Environment.NewLine}" +
            $"Task item id:{Environment.NewLine}{taskItem.ProjectPlanTaskItemId}{Environment.NewLine}{Environment.NewLine}" +
            $"Scope entry files:{Environment.NewLine}{CodeSnifferDogJson.Serialize(taskItem.Files)}";
    }
}
