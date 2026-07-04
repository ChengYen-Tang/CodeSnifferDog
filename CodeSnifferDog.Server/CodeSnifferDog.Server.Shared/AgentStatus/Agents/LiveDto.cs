namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Represents an agent payload included in a live update.
/// </summary>
public sealed class LiveDto
{
    /// <summary>
    /// Gets the agent identifier.
    /// </summary>
    public required Guid AgentId { get; init; }

    /// <summary>
    /// Gets the owning group identifier.
    /// </summary>
    public required Guid GroupId { get; init; }

    /// <summary>
    /// Gets the runtime key emitted by the execution runtime.
    /// </summary>
    public required string RuntimeKey { get; init; }

    /// <summary>
    /// Gets the user-facing agent name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the system prompt assigned to the agent.
    /// </summary>
    public string SystemPrompt { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current run status.
    /// </summary>
    public required RunStatus Status { get; init; }

    /// <summary>
    /// Gets when the agent was created.
    /// </summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
