using Microsoft.Agents.AI.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;

namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextAgentCompactionOptions
{
    public required OperationalContextChatReducer Reducer { get; init; }

    public required CompactionTrigger AutomaticCompactionTrigger { get; init; }

    public bool EnableReactiveCompactionRetry { get; init; } = true;

    public IOperationalContextReactiveCompactionExceptionDecider ReactiveExceptionDecider { get; init; } =
        new DefaultOperationalContextReactiveCompactionExceptionDecider();
}
