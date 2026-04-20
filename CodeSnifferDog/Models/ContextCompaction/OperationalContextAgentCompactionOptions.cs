using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;

namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextAgentCompactionOptions
{
    public required OperationalContextChatReducer Reducer { get; init; }

    public OperationalContextCollapseController? CollapseController { get; init; }

    public OperationalContextMessageShrinker MessageShrinker { get; init; } = new();

    public bool EnableReactiveCompactionRetry { get; init; } = true;

    public IOperationalContextReactiveCompactionExceptionDecider ReactiveExceptionDecider { get; init; } =
        new DefaultOperationalContextReactiveCompactionExceptionDecider();
}
