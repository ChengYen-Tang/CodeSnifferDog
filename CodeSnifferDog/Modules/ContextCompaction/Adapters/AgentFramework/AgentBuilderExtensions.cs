using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

/// <summary>
/// Registers context-compaction behavior into an <see cref="AIAgentBuilder" />.
/// </summary>
public static class AgentBuilderExtensions
{
    /// <summary>
    /// Adds message preparation, runtime compaction handling, and streaming retry behavior to the supplied agent builder.
    /// </summary>
    /// <param name="builder">Agent builder to configure.</param>
    /// <param name="options">Compaction options that define message preparation and runtime retry behavior.</param>
    /// <returns>The configured agent builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder" />, <paramref name="options" />, <paramref name="options.Reducer" />, or <paramref name="options.ReactiveExceptionDecider" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Context-collapse mode is enabled but no collapse controller was configured.</exception>
    public static AIAgentBuilder UseOperationalContextCompaction(
        this AIAgentBuilder builder,
        AgentCompactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Reducer);
        ArgumentNullException.ThrowIfNull(options.ReactiveExceptionDecider);

        if (options.Reducer.Options.Mode == CompactionMode.ContextCollapse &&
            options.CollapseController is null)
            throw new InvalidOperationException("ContextCollapse mode requires an CollapseController.");

        builder.UseAIContextProviders(new MessageContextProvider(options));

        return builder.Use(
            (messages, session, runOptions, innerAgent, cancellationToken) =>
                CompactionRuntime.RunAsync(messages, session, runOptions, innerAgent, options, cancellationToken),
            (messages, session, runOptions, innerAgent, cancellationToken) =>
                CompactionRuntime.RunStreamingAsync(messages, session, runOptions, innerAgent, options, cancellationToken));
    }

    /// <summary>
    /// Invokes a delegate with one reactive compaction retry when the model failure is retryable.
    /// </summary>
    /// <param name="messages">Original request messages.</param>
    /// <param name="options">Compaction options that define retry policy and retry preparation behavior.</param>
    /// <param name="next">Delegate that performs the actual invocation.</param>
    /// <param name="cancellationToken">Cancels the invocation.</param>
    /// <returns>A task that completes when the initial invocation or retry finishes.</returns>
    public static Task InvokeWithReactiveCompactionRetryAsync(
        IReadOnlyList<ChatMessage> messages,
        AgentCompactionOptions options,
        Func<IReadOnlyList<ChatMessage>, CancellationToken, Task> next,
        CancellationToken cancellationToken) =>
        ReactiveRetryService.InvokeAsync(messages, options, next, cancellationToken);

    /// <summary>
    /// Compares two message lists using the retry-equivalence rules.
    /// </summary>
    /// <param name="left">First message list.</param>
    /// <param name="right">Second message list.</param>
    /// <returns><see langword="true" /> when the messages are equivalent for retry suppression.</returns>
    internal static bool MessagesAreEquivalentForRetry(
        IReadOnlyList<ChatMessage> left,
        IReadOnlyList<ChatMessage> right) =>
        MessageEquivalenceComparer.AreEquivalent(left, right);
}
