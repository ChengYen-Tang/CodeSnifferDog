using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed class AgentStatusHistoryState(Guid? agentId, IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries, bool isLoading)
{
    public Guid? AgentId { get; private set; } = agentId;

    public IReadOnlyList<ProjectAgentTimelineEntryDto> TimelineEntries { get; private set; } = timelineEntries;

    public bool IsLoading { get; private set; } = isLoading;

    public string? ErrorMessage { get; private set; }

    public void ApplySnapshot(ProjectAgentStatusSnapshotDto? snapshot, Guid? selectedAgentId)
    {
        if (snapshot is null || selectedAgentId is null)
        {
            AgentId = null;
            TimelineEntries = [];
            IsLoading = false;
            ErrorMessage = null;
            return;
        }

        ApplySelectedAgentSnapshot(
            snapshot.AgentGroups
                .SelectMany(group => group.Agents)
                .FirstOrDefault(agent => agent.AgentId == selectedAgentId)?.TimelineEntries ?? [],
            selectedAgentId.Value);
    }

    public void ApplySelectedAgentSnapshot(IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries, Guid agentId)
    {
        AgentId = agentId;
        TimelineEntries = timelineEntries;
        IsLoading = false;
        ErrorMessage = null;
    }

    public void StartLoading(Guid agentId)
    {
        AgentId = agentId;
        TimelineEntries = [];
        IsLoading = true;
        ErrorMessage = null;
    }

    public void FinishLoading(Guid agentId)
    {
        if (AgentId == agentId)
            IsLoading = false;
    }

    public void SetError(Guid agentId, string errorMessage)
    {
        AgentId = agentId;
        TimelineEntries = [];
        IsLoading = false;
        ErrorMessage = errorMessage;
    }

    public void ClearError()
    {
        ErrorMessage = null;
    }

    public long GetLatestSequence() => TimelineEntries.Count == 0 ? 0 : TimelineEntries.Max(entry => entry.Sequence);
}