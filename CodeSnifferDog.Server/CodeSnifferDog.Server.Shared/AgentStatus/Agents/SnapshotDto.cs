namespace CodeSnifferDog.Server.Shared.AgentStatus.Agents;

public sealed class SnapshotDto
{
    public required Guid AgentId { get; init; }

    public required Guid GroupId { get; init; }

    public required string RuntimeKey { get; init; }

    public required string DisplayName { get; init; }

    public string SystemPrompt { get; init; } = string.Empty;

    public required RunStatus Status { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required bool HasLoadedHistory { get; init; }

    public required IReadOnlyList<TimelineEntryDto> TimelineEntries { get; init; }
}
