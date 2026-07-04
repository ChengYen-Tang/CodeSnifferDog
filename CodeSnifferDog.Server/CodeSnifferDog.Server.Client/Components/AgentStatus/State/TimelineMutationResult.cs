using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

/// <summary>
/// Holds a rewritten timeline entry list together with its latest observed sequence number.
/// </summary>
/// <param name="TimelineEntries">Rewritten timeline entries after the mutation.</param>
/// <param name="LatestSequence">Latest sequence number present after the mutation.</param>
internal sealed record TimelineMutationResult(
    IReadOnlyList<TimelineEntryDto> TimelineEntries,
    long LatestSequence);
