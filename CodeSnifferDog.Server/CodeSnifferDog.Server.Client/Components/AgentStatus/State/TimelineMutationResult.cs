using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed record TimelineMutationResult(
    IReadOnlyList<ProjectAgentTimelineEntryDto> TimelineEntries,
    long LatestSequence);
