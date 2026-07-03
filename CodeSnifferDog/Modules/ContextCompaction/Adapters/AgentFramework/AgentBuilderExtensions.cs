using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public static class AgentBuilderExtensions
{
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

    public static Task InvokeWithReactiveCompactionRetryAsync(
        IReadOnlyList<ChatMessage> messages,
        AgentCompactionOptions options,
        Func<IReadOnlyList<ChatMessage>, CancellationToken, Task> next,
        CancellationToken cancellationToken) =>
        ReactiveRetryService.InvokeAsync(messages, options, next, cancellationToken);

    internal static bool MessagesAreEquivalentForRetry(
        IReadOnlyList<ChatMessage> left,
        IReadOnlyList<ChatMessage> right) =>
        MessageEquivalenceComparer.AreEquivalent(left, right);
}
