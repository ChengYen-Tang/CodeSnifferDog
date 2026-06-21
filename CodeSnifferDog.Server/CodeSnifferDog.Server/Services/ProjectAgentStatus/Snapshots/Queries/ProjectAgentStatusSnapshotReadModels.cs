using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

internal sealed record ProjectAgentStatusSnapshotReadModel(
    Guid ProjectId,
    ProjectProcessingStatus ProjectStatus,
    IReadOnlyList<ProjectAgentStatusSnapshotGroupRow> Groups);

internal sealed record ProjectAgentStatusSnapshotGroupRow(
    AgentStatusGroupProjection Group,
    IReadOnlyList<ProjectAgentStatusSnapshotAgentRow> Agents);

internal sealed record ProjectAgentStatusSnapshotAgentRow(
    AgentStatusAgentProjection Agent,
    bool HasLoadedHistory,
    IReadOnlyList<AgentStatusTimelineEntryProjection> TimelineEntries);

internal sealed record ProjectAgentHistorySnapshotReadModel(
    Guid ProjectId,
    Guid AgentId,
    IReadOnlyList<AgentStatusTimelineEntryProjection> TimelineEntries);
