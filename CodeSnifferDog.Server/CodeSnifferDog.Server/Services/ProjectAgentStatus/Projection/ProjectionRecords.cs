using CodeSnifferDog.Server.Data.Entities;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

internal sealed record GroupProjection(
    Guid GroupId,
    string RuntimeKey,
    string DisplayName,
    DateTimeOffset CreatedAtUtc);

internal sealed record AgentProjection(
    Guid AgentId,
    Guid GroupId,
    string RuntimeKey,
    string DisplayName,
    string SystemPrompt,
    PersistedAgentStatus Status,
    DateTimeOffset CreatedAtUtc);

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
