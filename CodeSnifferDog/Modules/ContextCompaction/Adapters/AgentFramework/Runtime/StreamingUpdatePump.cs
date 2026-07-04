using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Threading.Channels;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

/// <summary>
/// Buffers streaming response updates from an inner agent so runtime retry logic can control completion separately.
/// </summary>
/// <param name="innerAgent">Inner agent that produces streaming updates.</param>
/// <param name="session">Agent session associated with the invocation.</param>
/// <param name="runOptions">Optional run options passed to the inner agent.</param>
/// <param name="cancellationToken">Cancellation token shared across the invocation and reader.</param>
internal sealed class StreamingUpdatePump(
    AIAgent innerAgent,
    AgentSession? session,
    AgentRunOptions? runOptions,
    CancellationToken cancellationToken)
{
    private readonly Channel<AgentResponseUpdate> _updates = Channel.CreateBounded<AgentResponseUpdate>(1);

    /// <summary>
    /// Reads all buffered response updates until the pump completes.
    /// </summary>
    /// <returns>The async stream of buffered response updates.</returns>
    public IAsyncEnumerable<AgentResponseUpdate> ReadAllAsync() =>
        _updates.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Runs one streaming invocation and forwards each update into the local channel.
    /// </summary>
    /// <param name="currentMessages">Messages to send to the inner agent.</param>
    public async Task PumpAsync(IEnumerable<ChatMessage> currentMessages)
    {
        await foreach (AgentResponseUpdate update in innerAgent
            .RunStreamingAsync(currentMessages, session, runOptions, cancellationToken)
            .ConfigureAwait(false))
            await _updates.Writer.WriteAsync(update, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Completes the update channel, optionally with an error.
    /// </summary>
    /// <param name="error">Optional terminal error that should fault the reader.</param>
    public void Complete(Exception? error) =>
        _updates.Writer.TryComplete(error);
}
