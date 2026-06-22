using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Threading.Channels;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

internal sealed class StreamingUpdatePump(
    AIAgent innerAgent,
    AgentSession? session,
    AgentRunOptions? runOptions,
    CancellationToken cancellationToken)
{
    private readonly Channel<AgentResponseUpdate> _updates = Channel.CreateBounded<AgentResponseUpdate>(1);

    public IAsyncEnumerable<AgentResponseUpdate> ReadAllAsync() =>
        _updates.Reader.ReadAllAsync(cancellationToken);

    public async Task PumpAsync(IEnumerable<ChatMessage> currentMessages)
    {
        await foreach (AgentResponseUpdate update in innerAgent
            .RunStreamingAsync(currentMessages, session, runOptions, cancellationToken)
            .ConfigureAwait(false))
            await _updates.Writer.WriteAsync(update, cancellationToken).ConfigureAwait(false);
    }

    public void Complete(Exception? error) =>
        _updates.Writer.TryComplete(error);
}
