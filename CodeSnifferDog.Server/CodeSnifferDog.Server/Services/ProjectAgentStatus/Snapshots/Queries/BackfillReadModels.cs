using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

/// <summary>
/// Read model used to construct a live-update backfill response.
/// </summary>
/// <param name="ProjectId">Project identifier being backfilled.</param>
/// <param name="ProjectStatus">Current persisted project status, when the project exists.</param>
/// <param name="Groups">Persisted agent-group projections.</param>
/// <param name="Agents">Persisted agent projections.</param>
/// <param name="TimelineEntries">Timeline entries newer than the client's latest known sequence.</param>
internal sealed record BackfillReadModel(
    Guid ProjectId,
    ProjectProcessingStatus? ProjectStatus,
    IReadOnlyList<GroupProjection> Groups,
    IReadOnlyList<AgentProjection> Agents,
    IReadOnlyList<TimelineEntryProjection> TimelineEntries);
