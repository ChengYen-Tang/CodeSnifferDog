using CodeSnifferDog.Modules.ContextCompaction.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;

internal static class ReactiveRetryService
{
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

    public static bool ShouldRetry(
        AgentCompactionOptions options,
        ModelInvocationException exception) =>
        options.EnableReactiveCompactionRetry &&
        options.ReactiveExceptionDecider.ShouldRetryWithReactiveCompaction(exception);

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
