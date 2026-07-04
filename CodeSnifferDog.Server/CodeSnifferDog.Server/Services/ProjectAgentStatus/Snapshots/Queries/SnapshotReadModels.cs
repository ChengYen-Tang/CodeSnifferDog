using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

/// <summary>
/// Read model used to build a full project-agent status snapshot.
/// </summary>
/// <param name="ProjectId">Project identifier.</param>
/// <param name="ProjectStatus">Persisted project status.</param>
/// <param name="Groups">Projected agent-group rows.</param>
internal sealed record SnapshotReadModel(
    Guid ProjectId,
    ProjectProcessingStatus ProjectStatus,
    IReadOnlyList<SnapshotGroupRow> Groups);

/// <summary>
/// Read model that pairs one group projection with its agent rows.
/// </summary>
/// <param name="Group">Projected group row.</param>
/// <param name="Agents">Projected agent rows inside the group.</param>
internal sealed record SnapshotGroupRow(
    GroupProjection Group,
    IReadOnlyList<SnapshotAgentRow> Agents);

/// <summary>
/// Read model that pairs one agent projection with its optional loaded history.
/// </summary>
/// <param name="Agent">Projected agent row.</param>
/// <param name="HasLoadedHistory">Whether <paramref name="TimelineEntries" /> is populated for this agent.</param>
/// <param name="TimelineEntries">Loaded timeline entries for the agent.</param>
internal sealed record SnapshotAgentRow(
    AgentProjection Agent,
    bool HasLoadedHistory,
    IReadOnlyList<TimelineEntryProjection> TimelineEntries);

/// <summary>
/// Read model used to build the full history snapshot for one agent.
/// </summary>
/// <param name="ProjectId">Project identifier that owns the agent.</param>
/// <param name="AgentId">Agent identifier whose history was loaded.</param>
/// <param name="TimelineEntries">Loaded timeline entries.</param>
internal sealed record HistorySnapshotReadModel(
    Guid ProjectId,
    Guid AgentId,
    IReadOnlyList<TimelineEntryProjection> TimelineEntries);
