using CodeSnifferDog.Models.ContextCompaction.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

/// <summary>Agent-framework adapter for <see cref="ContextPreparationService"/>.</summary>
public sealed class MessageContextProvider : MessageAIContextProvider
{
    private readonly ContextPreparationService _preparation;

    public MessageContextProvider(AgentCompactionOptions agentOptions) =>
        _preparation = new ContextPreparationService(agentOptions);

    /// <inheritdoc />
    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(
        InvokingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return await _preparation.PrepareAsync(context.RequestMessages, context.Session, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
