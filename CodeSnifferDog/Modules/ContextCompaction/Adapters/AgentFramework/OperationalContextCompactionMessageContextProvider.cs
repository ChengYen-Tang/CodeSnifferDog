using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using CodeSnifferDog.Modules.ContextCompaction.Core;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public sealed class OperationalContextCompactionMessageContextProvider : MessageAIContextProvider
{
    private readonly CompactionStrategy _strategy;

    public OperationalContextCompactionMessageContextProvider(
        OperationalContextChatReducer reducer,
        CompactionTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(reducer);
        ArgumentNullException.ThrowIfNull(trigger);

        _strategy = OperationalContextCompactionStrategyFactory.Create(reducer, trigger);
    }

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(
        InvokingContext context,
    CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return await CompactionProvider.CompactAsync(
            _strategy,
            context.RequestMessages,
            NullLogger.Instance,
            cancellationToken).ConfigureAwait(false);
    }
}
