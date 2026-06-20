using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

internal sealed record AgentTimelineEntryMutationResult(ProjectAgentTimelineEntryRecord Entry);

internal sealed record AgentTimelineRemovalMutationResult(
    Guid AgentId,
    IReadOnlyList<Guid> RemovedEntryIds,
    DateTimeOffset OccurredAtUtc);
