using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed record AgentStatusTimelineMutationResult(
    IReadOnlyList<ProjectAgentTimelineEntryDto> TimelineEntries,
    long LatestSequence);
