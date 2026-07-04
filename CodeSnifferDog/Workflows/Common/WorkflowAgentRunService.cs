using CodeSnifferDog.Models.ReviewAgentTeam;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Wraps guarded agent execution with workflow-specific status transitions.
/// </summary>
internal static class WorkflowAgentRunService
{
    /// <summary>
    /// Publishes running/completed status transitions around <see cref="AgentRunGuard.RunAsync{TSnapshot}(AIAgent, Func{AIAgent}, Func{Guid, TSnapshot}, Action{TSnapshot}, List{ChatMessage}, IAgentEventScope, int, TimeSpan, int, CancellationToken)" />.
    /// </summary>
    /// <typeparam name="TAttemptState">Type that captures restorable state for one agent attempt.</typeparam>
    /// <param name="agent">Current agent instance that will execute the run.</param>
    /// <param name="agentFactory">Creates a fresh agent instance after one failed attempt.</param>
    /// <param name="prepareAttempt">Captures state before each attempt starts.</param>
    /// <param name="restoreAttempt">Restores state captured by <paramref name="prepareAttempt" />.</param>
    /// <param name="messages">Conversation messages sent to and extended by the agent.</param>
    /// <param name="eventScope">Event scope that receives status and transcript events.</param>
    /// <param name="publishedMessageCount">Count of user messages already mirrored to the event stream.</param>
    /// <param name="timeout">Maximum duration allowed for one attempt.</param>
    /// <param name="maxConsecutiveFailures">Maximum number of failed attempts before returning a failure result. Zero disables the cap.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The run result, updated published-message count, and the last usable agent instance.</returns>
    public static async Task<(Result Result, int PublishedMessageCount, AIAgent Agent)> RunAsync<TAttemptState>(
        AIAgent agent,
        Func<AIAgent> agentFactory,
        Func<Guid, TAttemptState> prepareAttempt,
        Action<TAttemptState> restoreAttempt,
        List<ChatMessage> messages,
        IAgentEventScope eventScope,
        int publishedMessageCount,
        TimeSpan timeout,
        int maxConsecutiveFailures,
        CancellationToken cancellationToken)
    {
        await eventScope.PublishStatusChangedAsync(AgentStatusCatalog.RunningStatus, cancellationToken)
            .ConfigureAwait(false);

        (Result result, int updatedPublishedMessageCount, AIAgent updatedAgent) = await AgentRunGuard.RunAsync(
            agent,
            agentFactory,
            prepareAttempt,
            restoreAttempt,
            messages,
            eventScope,
            publishedMessageCount,
            timeout,
            maxConsecutiveFailures,
            cancellationToken).ConfigureAwait(false);

        await eventScope.PublishStatusChangedAsync(
            result.IsFailed ? AgentStatusCatalog.DegradedStatus : AgentStatusCatalog.CompletedStatus,
            cancellationToken).ConfigureAwait(false);

        return (result, updatedPublishedMessageCount, updatedAgent);
    }
}
