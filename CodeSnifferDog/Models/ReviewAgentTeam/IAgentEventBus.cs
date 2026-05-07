namespace CodeSnifferDog.Models.ReviewAgentTeam;

public interface IAgentEventBus
{
    IAgentEventScope CreateScope(string groupKey, string agentKey);

    ValueTask PublishGroupCreatedAsync(
        string groupKey,
        string displayName,
        CancellationToken cancellationToken = default);
}
