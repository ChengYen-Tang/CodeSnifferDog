namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Represents an agent run-status transition.
/// </summary>
public sealed class StatusChangedDto
{
    /// <summary>
    /// Gets the agent identifier.
    /// </summary>
    public required Guid AgentId { get; init; }

    /// <summary>
    /// Gets the current run status.
    /// </summary>
    public required RunStatus Status { get; init; }

    /// <summary>
    /// Gets when the status transition occurred.
    /// </summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }
}
