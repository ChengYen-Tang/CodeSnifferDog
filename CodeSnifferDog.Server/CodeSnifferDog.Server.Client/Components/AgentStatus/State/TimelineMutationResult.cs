using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed record TimelineMutationResult(
    IReadOnlyList<TimelineEntryDto> TimelineEntries,
    long LatestSequence);
