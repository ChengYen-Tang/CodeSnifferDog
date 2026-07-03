using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed class HistoryState(Guid? agentId, IReadOnlyList<TimelineEntryDto> timelineEntries, bool isLoading)
{
    private long _latestSequence = TimelineEntryList.GetLatestSequence(timelineEntries);

    public Guid? AgentId { get; private set; } = agentId;

    public IReadOnlyList<TimelineEntryDto> TimelineEntries { get; private set; } = timelineEntries;

    public bool IsLoading { get; private set; } = isLoading;

    public string? ErrorMessage { get; private set; }

    public void ApplySnapshot(SnapshotState snapshot, Guid? selectedAgentId)
    {
        if (snapshot.Snapshot is null || selectedAgentId is null)
        {
            AgentId = null;
            TimelineEntries = [];
            _latestSequence = 0;
            IsLoading = false;
            ErrorMessage = null;
            return;
        }

        ApplySelectedAgentSnapshot(snapshot.GetHistory(selectedAgentId.Value), selectedAgentId.Value);
    }

    public void ApplySelectedAgentSnapshot(IReadOnlyList<TimelineEntryDto> timelineEntries, Guid agentId)
    {
        ApplySelectedAgentSnapshot(
            timelineEntries,
            agentId,
            TimelineEntryList.GetLatestSequence(timelineEntries));
    }

    public void ApplySelectedAgentSnapshot(
        IReadOnlyList<TimelineEntryDto> timelineEntries,
        Guid agentId,
        long latestSequence)
    {
        AgentId = agentId;
        TimelineEntries = timelineEntries;
        _latestSequence = latestSequence;
        IsLoading = false;
        ErrorMessage = null;
    }

    public void StartLoading(Guid agentId)
    {
        AgentId = agentId;
        TimelineEntries = [];
        _latestSequence = 0;
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
        _latestSequence = 0;
        IsLoading = false;
        ErrorMessage = errorMessage;
    }

    public void ClearError()
    {
        ErrorMessage = null;
    }

    public long GetLatestSequence() => _latestSequence;

}
