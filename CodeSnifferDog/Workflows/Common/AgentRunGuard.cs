using CodeSnifferDog.Models.ReviewAgentTeam;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Modules.ReviewAgentTeam.Transcript;

namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Executes one agent run with attempt snapshots, timeout handling, and transcript cleanup for failed attempts.
/// </summary>
internal static class AgentRunGuard
{
    /// <summary>
    /// Runs one agent conversation, recreating the agent between failed attempts when configured to do so.
    /// </summary>
    /// <typeparam name="TSnapshot">Type that captures the mutable state needed to restore one failed attempt.</typeparam>
    /// <param name="agent">Current agent instance that will execute the run.</param>
    /// <param name="agentFactory">Creates a fresh agent instance after one failed attempt.</param>
    /// <param name="prepareAttempt">Captures a snapshot for the current attempt.</param>
    /// <param name="restoreAttempt">Restores the snapshot captured by <paramref name="prepareAttempt" />.</param>
    /// <param name="messages">Conversation messages sent to and extended by the agent.</param>
    /// <param name="eventScope">Event scope that receives transcript and status events.</param>
    /// <param name="publishedMessageCount">Count of user messages already mirrored to the event stream.</param>
    /// <param name="timeout">Maximum duration allowed for one attempt.</param>
    /// <param name="maxConsecutiveFailures">Maximum number of failed attempts before returning a failure result. Zero disables the cap.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The final run result, updated published-message count, and the last usable agent instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout" /> is not positive, or <paramref name="maxConsecutiveFailures" /> is negative.</exception>
    public static async Task<(Result Result, int PublishedMessageCount, AIAgent Agent)> RunAsync<TSnapshot>(
        AIAgent agent,
        Func<AIAgent> agentFactory,
        Func<Guid, TSnapshot> prepareAttempt,
        Action<TSnapshot> restoreAttempt,
        List<ChatMessage> messages,
        IAgentEventScope eventScope,
        int publishedMessageCount,
        TimeSpan timeout,
        int maxConsecutiveFailures,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(agentFactory);
        ArgumentNullException.ThrowIfNull(prepareAttempt);
        ArgumentNullException.ThrowIfNull(restoreAttempt);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(eventScope);

        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Agent run timeout must be greater than zero.");
        if (maxConsecutiveFailures < 0)
            throw new ArgumentOutOfRangeException(nameof(maxConsecutiveFailures), "Max consecutive failures must be zero or greater.");

        Exception? lastException = null;

        for (int attempt = 1; maxConsecutiveFailures == 0 || attempt <= maxConsecutiveFailures; attempt++)
        {
            Guid attemptId = Guid.CreateVersion7();
            TSnapshot snapshot = prepareAttempt(attemptId);
            DateTimeOffset attemptRunStartedAtUtc = DateTimeOffset.MinValue;

            try
            {
                publishedMessageCount = await PublishPendingUserMessagesAsync(
                    messages,
                    eventScope,
                    publishedMessageCount,
                    cancellationToken).ConfigureAwait(false);

                using CancellationTokenSource timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutTokenSource.CancelAfter(timeout);

                attemptRunStartedAtUtc = DateTimeOffset.UtcNow;
                AgentResponse response = await AgentRunAttemptContext.RunAsync(
                    attemptId,
                    eventScope.GroupKey,
                    eventScope.AgentKey,
                    () => agent.RunAsync(
                        messages,
                        session: null,
                        options: null,
                        timeoutTokenSource.Token)).ConfigureAwait(false);

                bool transcriptEventsPublished =
                    AgentBuilderExtensions.HasPublishedTranscriptEvents(response);

                foreach (ChatMessage message in response.Messages)
                {
                    messages.Add(message);
                    if (transcriptEventsPublished)
                        continue;

                    await AgentToolEventPublisher.PublishAsync(message, eventScope, cancellationToken).ConfigureAwait(false);
                    if (message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Text))
                        await eventScope.PublishAssistantMessageAsync(message.Text, cancellationToken).ConfigureAwait(false);
                }

                return (Result.Ok(), messages.Count, agent);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                restoreAttempt(snapshot);
                throw;
            }
            catch (OperationCanceledException ex)
            {
                restoreAttempt(snapshot);
                await ClearAttemptTranscriptAsync(eventScope, attemptRunStartedAtUtc, cancellationToken).ConfigureAwait(false);
                lastException = new TimeoutException(
                    $"Agent run attempt {attempt} timed out after {timeout}.",
                    ex);
            }
            catch (Exception ex)
            {
                restoreAttempt(snapshot);
                await ClearAttemptTranscriptAsync(eventScope, attemptRunStartedAtUtc, cancellationToken).ConfigureAwait(false);
                lastException = ex;
            }

            agent = agentFactory();
        }

        return (Result.Fail(new ExceptionalError(
            $"Agent run failed after {maxConsecutiveFailures} consecutive attempts: {lastException}",
            lastException!)), publishedMessageCount, agent);
    }

    /// <summary>
    /// Clears transcript events published after an attempt started so the next attempt can replay a clean transcript.
    /// </summary>
    /// <param name="eventScope">Event scope that owns the transcript.</param>
    /// <param name="attemptRunStartedAtUtc">Timestamp recorded when the attempt started running.</param>
    /// <param name="cancellationToken">Cancels transcript cleanup.</param>
    private static ValueTask ClearAttemptTranscriptAsync(
        IAgentEventScope eventScope,
        DateTimeOffset attemptRunStartedAtUtc,
        CancellationToken cancellationToken) =>
        attemptRunStartedAtUtc == DateTimeOffset.MinValue
            ? ValueTask.CompletedTask
            : eventScope.PublishTranscriptClearedAsync(attemptRunStartedAtUtc, cancellationToken);

    /// <summary>
    /// Publishes any user messages that were appended since the last successful publish checkpoint.
    /// </summary>
    /// <param name="messages">Conversation messages that may contain unpublished user messages.</param>
    /// <param name="eventScope">Event scope that receives published user messages.</param>
    /// <param name="publishedMessageCount">Number of messages already published.</param>
    /// <param name="cancellationToken">Cancels publication.</param>
    /// <returns>The new published-message count.</returns>
    private static async Task<int> PublishPendingUserMessagesAsync(
        List<ChatMessage> messages,
        IAgentEventScope eventScope,
        int publishedMessageCount,
        CancellationToken cancellationToken)
    {
        for (int index = publishedMessageCount; index < messages.Count; index++)
        {
            ChatMessage message = messages[index];
            if (message.Role == ChatRole.User && !string.IsNullOrWhiteSpace(message.Text))
                await eventScope.PublishUserMessageAsync(message.Text, cancellationToken).ConfigureAwait(false);
        }

        return messages.Count;
    }
}
