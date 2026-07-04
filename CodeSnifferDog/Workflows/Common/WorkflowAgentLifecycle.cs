using CodeSnifferDog.Models.ReviewAgentTeam;
using FluentResults;

namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Creates agents and publishes their initial lifecycle events.
/// </summary>
internal static class WorkflowAgentLifecycle
{
    /// <summary>
    /// Creates one agent, publishes its created event, and returns the created agent result.
    /// </summary>
    /// <param name="factory">Factory that constructs the agent and its system prompt.</param>
    /// <param name="agentName">Logical agent name used in creation-failure messages.</param>
    /// <param name="eventScope">Event scope that receives the created event.</param>
    /// <param name="displayName">Display name published for the agent.</param>
    /// <param name="cancellationToken">Cancels event publication.</param>
    /// <returns>The created agent result, or a failure result if creation threw.</returns>
    public static async Task<Result<AgentCreationResult>> CreateAndPublishAsync(
        Func<AgentCreationResult> factory,
        string agentName,
        IAgentEventScope eventScope,
        string displayName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Result<AgentCreationResult> createResult = WorkflowAgentCreation.TryCreate(factory, agentName);

        if (createResult.IsFailed)
            return createResult;

        await eventScope.PublishCreatedAsync(
            displayName,
            createResult.Value.SystemPrompt,
            AgentStatusCatalog.WaitingStatus,
            cancellationToken).ConfigureAwait(false);

        return createResult;
    }
}
