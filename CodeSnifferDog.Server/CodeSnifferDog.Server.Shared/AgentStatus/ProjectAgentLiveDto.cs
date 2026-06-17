namespace CodeSnifferDog.Server.Shared.AgentStatus;

public sealed class ProjectAgentLiveDto
{
    public required Guid AgentId { get; init; }

    public required Guid GroupId { get; init; }

    public required string RuntimeKey { get; init; }

    public required string DisplayName { get; init; }

    public string SystemPrompt { get; init; } = string.Empty;

    public required ProjectAgentRunStatus Status { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
}
