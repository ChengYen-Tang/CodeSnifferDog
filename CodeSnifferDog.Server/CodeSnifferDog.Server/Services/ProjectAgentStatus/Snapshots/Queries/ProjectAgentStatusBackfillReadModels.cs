using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

internal sealed record ProjectAgentStatusBackfillReadModel(
    Guid ProjectId,
    ProjectProcessingStatus? ProjectStatus,
    IReadOnlyList<AgentStatusGroupProjection> Groups,
    IReadOnlyList<AgentStatusAgentProjection> Agents,
    IReadOnlyList<AgentStatusTimelineEntryProjection> TimelineEntries);
