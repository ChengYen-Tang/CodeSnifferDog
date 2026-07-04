using CodeSnifferDog.Server.Data.Entities;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

/// <summary>
/// Projection of one persisted agent group.
/// </summary>
/// <param name="GroupId">Group identifier.</param>
/// <param name="RuntimeKey">Stable runtime key.</param>
/// <param name="DisplayName">Display name shown to clients.</param>
/// <param name="CreatedAtUtc">Creation timestamp in UTC.</param>
internal sealed record GroupProjection(
    Guid GroupId,
    string RuntimeKey,
    string DisplayName,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Projection of one persisted agent row.
/// </summary>
/// <param name="AgentId">Agent identifier.</param>
/// <param name="GroupId">Owning group identifier.</param>
/// <param name="RuntimeKey">Stable runtime key.</param>
/// <param name="DisplayName">Display name shown to clients.</param>
/// <param name="SystemPrompt">System prompt assigned to the agent.</param>
/// <param name="Status">Persisted agent status.</param>
/// <param name="CreatedAtUtc">Creation timestamp in UTC.</param>
internal sealed record AgentProjection(
    Guid AgentId,
    Guid GroupId,
    string RuntimeKey,
    string DisplayName,
    string SystemPrompt,
    PersistedAgentStatus Status,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Projection of one persisted timeline entry.
/// </summary>
/// <param name="TimelineEntryId">Timeline entry identifier.</param>
/// <param name="AgentId">Owning agent identifier.</param>
/// <param name="Sequence">Monotonic sequence number.</param>
/// <param name="EntryType">Persisted timeline entry type.</param>
/// <param name="OccurredAtUtc">Occurrence timestamp in UTC.</param>
/// <param name="Message">Message text, when applicable.</param>
/// <param name="ToolCallId">Tool-call identifier, when applicable.</param>
/// <param name="ToolName">Tool name, when applicable.</param>
/// <param name="ToolArguments">Serialized tool arguments, when applicable.</param>
/// <param name="ToolResult">Serialized tool result, when applicable.</param>
internal sealed record TimelineEntryProjection(
    Guid TimelineEntryId,
    Guid AgentId,
    long Sequence,
    ProjectAgentTimelineEntryType EntryType,
    DateTimeOffset OccurredAtUtc,
    string? Message,
    string? ToolCallId,
    string? ToolName,
    string? ToolArguments,
    string? ToolResult);
