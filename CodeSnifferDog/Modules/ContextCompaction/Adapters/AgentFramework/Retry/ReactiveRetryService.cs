using CodeSnifferDog.Modules.ContextCompaction.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;

/// <summary>
/// Re-invokes agent work with reactive compaction when a model invocation fails for a retryable reason.
/// </summary>
internal static class ReactiveRetryService
{
    /// <summary>
    /// Executes the supplied delegate and retries once with reactively compacted messages when the failure is retryable.
    /// </summary>
    /// <param name="messages">Original request messages.</param>
    /// <param name="options">Compaction options that define retry policy and retry preparation behavior.</param>
    /// <param name="next">Delegate that performs the actual invocation.</param>
    /// <param name="cancellationToken">Cancels the invocation or retry preparation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="messages" />, <paramref name="options" />, or <paramref name="next" /> is <see langword="null" />.</exception>
    public static async Task InvokeAsync(
        IReadOnlyList<ChatMessage> messages,
        AgentCompactionOptions options,
        Func<IReadOnlyList<ChatMessage>, CancellationToken, Task> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(next);

        try
        {
            await next(messages, cancellationToken).ConfigureAwait(false);
        }
        catch (ModelInvocationException ex) when (ShouldRetry(options, ex))
        {
            ReactiveRetryPreparation retryPreparation = await PrepareAsync(
                messages,
                null,
                options,
                cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ChatMessage> compactedMessages = retryPreparation.Messages;

            if (MessageEquivalenceComparer.AreEquivalent(messages, compactedMessages))
                throw;

            await next(compactedMessages, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Determines whether a model invocation failure should trigger reactive compaction retry.
    /// </summary>
    /// <param name="options">Compaction options that expose the retry toggle and exception decider.</param>
    /// <param name="exception">Model invocation failure to evaluate.</param>
    /// <returns><see langword="true" /> when reactive retry is enabled and the exception decider accepts the failure.</returns>
    public static bool ShouldRetry(
        AgentCompactionOptions options,
        ModelInvocationException exception) =>
        options.EnableReactiveCompactionRetry &&
        options.ReactiveExceptionDecider.ShouldRetryWithReactiveCompaction(exception);

    /// <summary>
    /// Prepares the retry transcript for reactive compaction.
    /// </summary>
    /// <param name="originalMessages">Original request messages before retry preparation.</param>
    /// <param name="session">Optional agent session whose collapse state should be consulted.</param>
    /// <param name="options">Compaction options that determine whether collapse mode or standard mode is used.</param>
    /// <param name="cancellationToken">Cancels retry preparation.</param>
    /// <returns>The prepared retry messages.</returns>
    public static async Task<ReactiveRetryPreparation> PrepareAsync(
        IReadOnlyList<ChatMessage> originalMessages,
        AgentSession? session,
        AgentCompactionOptions options,
        CancellationToken cancellationToken)
    {
        if (options.Reducer.Options.Mode == CompactionMode.ContextCollapse)
            return new ReactiveRetryPreparation
            {
                Messages = await options.CollapseController!
                    .PrepareReactiveRetryAsync(originalMessages, session, cancellationToken)
                    .ConfigureAwait(false),
            };

        IReadOnlyList<ChatMessage> retryMessages = MessageShrinker.ApplySnip(originalMessages, options.Reducer.Options).Messages;
        CompactionResult result =
            await options.Reducer.CompactReactiveAsync(retryMessages, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ChatMessage> compactedMessages = ChatReducer.BuildMessages(result);

        return new ReactiveRetryPreparation
        {
            Messages = compactedMessages,
        };
    }
}
