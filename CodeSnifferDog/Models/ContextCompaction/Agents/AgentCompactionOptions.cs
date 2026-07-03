using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;

namespace CodeSnifferDog.Models.ContextCompaction.Agents;

public sealed class AgentCompactionOptions
{
    public required ChatReducer Reducer { get; init; }

    public CollapseController? CollapseController { get; init; }

    public MessageShrinker MessageShrinker { get; init; } = new();

    public bool EnableReactiveCompactionRetry { get; init; } = true;

    public IReactiveExceptionDecider ReactiveExceptionDecider { get; init; } =
        new DefaultReactiveExceptionDecider();
}
