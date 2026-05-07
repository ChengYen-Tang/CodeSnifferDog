namespace CodeSnifferDog.Models.ReviewAgentTeam;

public interface IAgentEventScope
{
    string GroupKey { get; }

    string AgentKey { get; }

    ValueTask PublishCreatedAsync(
        string displayName,
        string initialStatus,
        CancellationToken cancellationToken = default);

    ValueTask PublishStatusChangedAsync(
        string status,
        CancellationToken cancellationToken = default);

    ValueTask PublishCompactionAsync(CancellationToken cancellationToken = default);
}
