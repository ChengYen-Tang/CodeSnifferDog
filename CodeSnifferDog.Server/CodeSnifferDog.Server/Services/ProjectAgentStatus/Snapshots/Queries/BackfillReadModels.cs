using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

internal sealed record BackfillReadModel(
    Guid ProjectId,
    ProjectProcessingStatus? ProjectStatus,
    IReadOnlyList<GroupProjection> Groups,
    IReadOnlyList<AgentProjection> Agents,
    IReadOnlyList<TimelineEntryProjection> TimelineEntries);
