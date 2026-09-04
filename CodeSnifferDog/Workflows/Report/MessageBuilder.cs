using CodeSnifferDog.Json;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Workflows.Common;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.Report;

/// <summary>
/// Builds the chat messages used by the report workflow's aggregator and verifier agents.
/// </summary>
/// <param name="messageTemplates">Prompt-backed text fragments used to compose workflow messages.</param>
internal sealed class MessageBuilder(MessageTemplates messageTemplates)
{
    private readonly MessageTemplates _messageTemplates = messageTemplates;

    /// <summary>
    /// Creates aggregator messages with the system-controlled task context for one current flow.
    /// </summary>
    /// <param name="taskItem">Task item whose scope produced the current flow.</param>
    /// <param name="currentFlowIssueCount">Number of verified issues available through the read-only current-flow tools.</param>
    /// <returns>The aggregator conversation messages.</returns>
    public List<ChatMessage> CreateAggregatorMessages(
        StoredTaskItem taskItem,
        int currentFlowIssueCount)
        =>
    [
        new(ChatRole.User, BuildTaskContext(_messageTemplates.AggregatorInputPrefix, taskItem, currentFlowIssueCount)),
    ];

    /// <summary>
    /// Creates verifier messages from one stored report diff and its system-controlled task context.
    /// </summary>
    /// <param name="taskItem">Task item whose scope produced the current flow.</param>
    /// <param name="currentFlowIssueCount">Number of verified issues available through the read-only current-flow tools.</param>
    /// <param name="diff">Diff produced by the report aggregator.</param>
    /// <returns>The verifier conversation messages.</returns>
    public List<ChatMessage> CreateVerifierMessages(
        StoredTaskItem taskItem,
        int currentFlowIssueCount,
        Diff diff)
        =>
    [
        new(ChatRole.User, BuildVerifierInput(taskItem, currentFlowIssueCount, diff)),
    ];

    /// <summary>
    /// Creates the retry message used when the verifier finishes without publishing a verdict.
    /// </summary>
    /// <returns>The missing-verdict retry message.</returns>
    public ChatMessage CreateMissingVerifierVerdictMessage()
        =>
        new(ChatRole.User, WorkflowRetryMessages.MissingVerifierVerdictMessage);

    /// <summary>
    /// Builds a task-context payload that directs the aggregator to read the bounded current-flow issue source.
    /// </summary>
    /// <param name="taskItem">Task item whose scope produced the current flow.</param>
    /// <param name="currentFlowIssueCount">Number of verified issues available through the read-only current-flow tools.</param>
    /// <returns>The formatted aggregator input.</returns>
    private static string BuildTaskContext(
        string prefix,
        StoredTaskItem taskItem,
        int currentFlowIssueCount)
    {
        ArgumentNullException.ThrowIfNull(taskItem);
        ArgumentOutOfRangeException.ThrowIfNegative(currentFlowIssueCount);

        return $"{prefix}{Environment.NewLine}{Environment.NewLine}" +
            $"Task item id:{Environment.NewLine}{taskItem.ProjectPlanTaskItemId}{Environment.NewLine}{Environment.NewLine}" +
            $"Scope entry files:{Environment.NewLine}{CodeSnifferDogJson.Serialize(taskItem.Files)}{Environment.NewLine}{Environment.NewLine}" +
            $"Verified current-flow issue count:{Environment.NewLine}{currentFlowIssueCount}";
    }

    /// <summary>
    /// Builds the verifier payload from task context and one serialized diff.
    /// </summary>
    /// <param name="taskItem">Task item whose scope produced the current flow.</param>
    /// <param name="currentFlowIssueCount">Number of verified issues available through the read-only current-flow tools.</param>
    /// <param name="diff">Diff produced by the report aggregator.</param>
    /// <returns>The formatted verifier input.</returns>
    private string BuildVerifierInput(
        StoredTaskItem taskItem,
        int currentFlowIssueCount,
        Diff diff)
        =>
        $"{BuildTaskContext(_messageTemplates.VerifierInputPrefix, taskItem, currentFlowIssueCount)}{Environment.NewLine}{Environment.NewLine}" +
        $"Current report diff:{Environment.NewLine}{CodeSnifferDogJson.Serialize(diff)}";
}
