using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

internal sealed record SnapshotReadModel(
    Guid ProjectId,
    ProjectProcessingStatus ProjectStatus,
    IReadOnlyList<SnapshotGroupRow> Groups);

internal sealed record SnapshotGroupRow(
    GroupProjection Group,
    IReadOnlyList<SnapshotAgentRow> Agents);

internal sealed record SnapshotAgentRow(
    AgentProjection Agent,
    bool HasLoadedHistory,
    IReadOnlyList<TimelineEntryProjection> TimelineEntries);

internal sealed record HistorySnapshotReadModel(
    Guid ProjectId,
    Guid AgentId,
    IReadOnlyList<TimelineEntryProjection> TimelineEntries);
