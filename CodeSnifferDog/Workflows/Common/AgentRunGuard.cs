using CodeSnifferDog.Models.ReviewAgentTeam;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Modules.ReviewAgentTeam.Transcript;

namespace CodeSnifferDog.Workflows.Common;

internal static class AgentRunGuard
{
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
        if (maxConsecutiveFailures <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConsecutiveFailures), "Max consecutive failures must be greater than zero.");

        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxConsecutiveFailures; attempt++)
        {
            Guid attemptId = Guid.NewGuid();
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
                    () => agent.RunAsync(
                        messages,
                        session: null,
                        options: null,
                        timeoutTokenSource.Token)).ConfigureAwait(false);

                bool transcriptEventsPublished =
                    AgentTranscriptEventAgentBuilderExtensions.HasPublishedTranscriptEvents(response);

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

    private static ValueTask ClearAttemptTranscriptAsync(
        IAgentEventScope eventScope,
        DateTimeOffset attemptRunStartedAtUtc,
        CancellationToken cancellationToken) =>
        attemptRunStartedAtUtc == DateTimeOffset.MinValue
            ? ValueTask.CompletedTask
            : eventScope.PublishTranscriptClearedAsync(attemptRunStartedAtUtc, cancellationToken);

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
