using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed class AgentStatusPageState(
    AgentStatusSnapshotState snapshot,
    AgentStatusLiveConnectionState liveConnection,
    AgentStatusSelectionState selection,
    AgentStatusHistoryState history,
    AgentStatusSelectedAgentLiveConnectionState selectedAgentLiveConnection)
{
    private readonly AgentStatusLiveUpdateReducer _liveUpdateReducer = new();

    public AgentStatusSnapshotState Snapshot { get; } = snapshot;

    public AgentStatusLiveConnectionState LiveConnection { get; } = liveConnection;

    public AgentStatusSelectionState Selection { get; } = selection;

    public AgentStatusHistoryState History { get; } = history;

    public AgentStatusSelectedAgentLiveConnectionState SelectedAgentLiveConnection { get; } = selectedAgentLiveConnection;

    public bool IsSnapshotLoading { get; private set; }

    public string? SnapshotLoadErrorMessage { get; private set; }

    public string? LiveConnectionErrorMessage { get; private set; }

    public string SnapshotLoadStatusText =>
        SnapshotLoadErrorMessage is not null
            ? "Snapshot unavailable"
            : IsSnapshotLoading
                ? "Snapshot loading"
                : Snapshot.Snapshot is not null
                    ? "Snapshot loaded"
                    : "Idle";

    public AgentStatusCompletionState Completion =>
        AgentStatusCompletionState.From(Snapshot.Snapshot?.ProjectStatus, LiveConnection);

    public static AgentStatusPageState CreateEmpty() =>
        new(
            new AgentStatusSnapshotState(null),
            new AgentStatusLiveConnectionState(isConnected: false, isSubscribed: false, statusText: "Idle"),
            new AgentStatusSelectionState(selectedAgentId: null, expandedToolDetails: []),
            new AgentStatusHistoryState(agentId: null, timelineEntries: [], isLoading: false),
            new AgentStatusSelectedAgentLiveConnectionState(agentId: null, isConnected: false, isSubscribed: false, statusText: "Idle"));

    public void SetSnapshot(ProjectAgentStatusSnapshotDto? snapshot)
    {
        Snapshot.Replace(snapshot);
        Selection.ApplySnapshot(Snapshot);
        History.ApplySnapshot(Snapshot, Selection.SelectedAgentId);
        Snapshot.ReleaseHistoryExcept(Selection.SelectedAgentId);
    }

    public void SetLiveConnection(bool isConnected, bool isSubscribed, string statusText)
    {
        LiveConnection.Update(isConnected, isSubscribed, statusText);
    }

    public void SetSelectedAgentLiveConnection(Guid? agentId, bool isConnected, bool isSubscribed, string statusText)
    {
        SelectedAgentLiveConnection.Update(agentId, isConnected, isSubscribed, statusText);
    }

    public void SetLiveConnectionError(string? errorMessage)
    {
        LiveConnectionErrorMessage = errorMessage;
    }

    public void SetSnapshotLoadState(bool isLoading, string? errorMessage)
    {
        IsSnapshotLoading = isLoading;
        SnapshotLoadErrorMessage = errorMessage;
    }

    public bool ApplyLiveUpdate(ProjectAgentLiveUpdateDto update) =>
        _liveUpdateReducer.Apply(update, Snapshot, Selection, History);

    public void SetAgentHistory(Guid agentId, IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries)
    {
        Snapshot.ReplaceAgentHistory(agentId, timelineEntries);
        History.ApplySelectedAgentSnapshot(timelineEntries, agentId);
    }

    public void SelectAgent(Guid agentId)
    {
        Selection.SelectAgent(agentId);
        Snapshot.ReleaseHistoryExcept(agentId);
        SelectedAgentLiveConnection.Update(agentId, isConnected: false, isSubscribed: false, statusText: "Switching");
    }
}
