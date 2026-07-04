namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Represents an agent group inside a status snapshot.
/// </summary>
public sealed class GroupSnapshotDto
{
    /// <summary>
    /// Gets the group identifier.
    /// </summary>
    public required Guid GroupId { get; init; }

    /// <summary>
    /// Gets the runtime key emitted by the execution runtime.
    /// </summary>
    public required string RuntimeKey { get; init; }

    /// <summary>
    /// Gets the user-facing group name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets when the group was created.
    /// </summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// Gets the agents that belong to the group.
    /// </summary>
    public required IReadOnlyList<SnapshotDto> Agents { get; init; }
}
