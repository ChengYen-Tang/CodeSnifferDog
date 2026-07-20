using System.Text.Json.Serialization;

namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

/// <summary>
/// Represents an agent inside a status snapshot.
/// </summary>
public sealed class SnapshotDto
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
    /// Gets the system prompt assigned to the agent when this agent's details are loaded.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// Gets the current run status.
    /// </summary>
    public required RunStatus Status { get; init; }

    /// <summary>
    /// Gets when the agent was created.
    /// </summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// Gets a value indicating whether the agent's historical timeline has been loaded.
    /// </summary>
    public required bool HasLoadedHistory { get; init; }

    /// <summary>
    /// Gets the currently loaded timeline entries.
    /// </summary>
    public required IReadOnlyList<TimelineEntryDto> TimelineEntries { get; init; }
}
