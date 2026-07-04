using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

/// <summary>
/// Describes an appended or updated timeline entry mutation.
/// </summary>
/// <param name="Entry">Timeline entry affected by the mutation.</param>
internal sealed record TimelineEntryMutationResult(ProjectAgentTimelineEntryRecord Entry);

/// <summary>
/// Describes a transcript-removal mutation.
/// </summary>
/// <param name="AgentId">Agent whose timeline entries were removed.</param>
/// <param name="RemovedEntryIds">Identifiers of removed timeline entries.</param>
/// <param name="OccurredAtUtc">Timestamp associated with the removal event.</param>
internal sealed record TimelineRemovalMutationResult(
    Guid AgentId,
    IReadOnlyList<Guid> RemovedEntryIds,
    DateTimeOffset OccurredAtUtc);
