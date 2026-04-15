using Microsoft.Agents.AI.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Core;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public static class OperationalContextCompactionStrategyFactory
{
    public static CompactionStrategy Create(OperationalContextChatReducer reducer, CompactionTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(reducer);
        ArgumentNullException.ThrowIfNull(trigger);

        return new ChatReducerCompactionStrategy(reducer, trigger);
    }
}
