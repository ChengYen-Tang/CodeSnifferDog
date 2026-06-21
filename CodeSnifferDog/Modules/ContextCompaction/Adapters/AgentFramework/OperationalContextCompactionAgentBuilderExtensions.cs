using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public static class OperationalContextCompactionAgentBuilderExtensions
{
    public static AIAgentBuilder UseOperationalContextCompaction(
        this AIAgentBuilder builder,
        OperationalContextAgentCompactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Reducer);
        ArgumentNullException.ThrowIfNull(options.ReactiveExceptionDecider);

        if (options.Reducer.Options.Mode == OperationalContextCompactionMode.ContextCollapse &&
            options.CollapseController is null)
            throw new InvalidOperationException("ContextCollapse mode requires an OperationalContextCollapseController.");

        builder.UseAIContextProviders(new OperationalContextCompactionMessageContextProvider(options));

        return builder.Use(
            (messages, session, runOptions, innerAgent, cancellationToken) =>
                AgentFrameworkCompactionRuntime.RunAsync(messages, session, runOptions, innerAgent, options, cancellationToken),
            (messages, session, runOptions, innerAgent, cancellationToken) =>
                AgentFrameworkCompactionRuntime.RunStreamingAsync(messages, session, runOptions, innerAgent, options, cancellationToken));
    }

    public static Task InvokeWithReactiveCompactionRetryAsync(
        IReadOnlyList<ChatMessage> messages,
        OperationalContextAgentCompactionOptions options,
        Func<IReadOnlyList<ChatMessage>, CancellationToken, Task> next,
        CancellationToken cancellationToken) =>
        ReactiveRetryService.InvokeAsync(messages, options, next, cancellationToken);

    internal static bool MessagesAreEquivalentForRetry(
        IReadOnlyList<ChatMessage> left,
        IReadOnlyList<ChatMessage> right) =>
        MessageEquivalenceComparer.AreEquivalent(left, right);
}
