namespace CodeSnifferDog.Models.ReviewAgentTeam;

public interface IAgentStatusEventPublisher
{
    ValueTask PublishAsync(AgentStatusEvent agentEvent, CancellationToken cancellationToken = default);
}
