using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Modules.ReviewAgentTeam;

internal sealed class NoOpAgentStatusEventPublisher : IAgentStatusEventPublisher
{
    public static NoOpAgentStatusEventPublisher Instance { get; } = new();

    private NoOpAgentStatusEventPublisher()
    {
    }

    public ValueTask PublishAsync(AgentStatusEvent agentEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentEvent);
        return ValueTask.CompletedTask;
    }
}
