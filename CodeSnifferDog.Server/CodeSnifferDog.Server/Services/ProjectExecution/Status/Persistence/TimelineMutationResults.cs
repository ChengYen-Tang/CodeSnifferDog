using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

internal sealed record TimelineEntryMutationResult(ProjectAgentTimelineEntryRecord Entry);

internal sealed record TimelineRemovalMutationResult(
    Guid AgentId,
    IReadOnlyList<Guid> RemovedEntryIds,
    DateTimeOffset OccurredAtUtc);
