using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed class PageState(
    SnapshotState snapshot,
    LiveConnectionState liveConnection,
    SelectionState selection,
    HistoryState history,
    SelectedAgentLiveConnectionState selectedAgentLiveConnection)
{
    private readonly LiveUpdateReducer _liveUpdateReducer = new();

    public SnapshotState Snapshot { get; } = snapshot;

    public LiveConnectionState LiveConnection { get; } = liveConnection;

    public SelectionState Selection { get; } = selection;

    public HistoryState History { get; } = history;

    public SelectedAgentLiveConnectionState SelectedAgentLiveConnection { get; } = selectedAgentLiveConnection;

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

    public CompletionState Completion =>
        CompletionState.From(Snapshot.Snapshot?.ProjectStatus, LiveConnection);

    public static PageState CreateEmpty() =>
        new(
            new SnapshotState(null),
            new LiveConnectionState(isConnected: false, isSubscribed: false, statusText: "Idle"),
            new SelectionState(selectedAgentId: null, expandedToolDetails: []),
            new HistoryState(agentId: null, timelineEntries: [], isLoading: false),
            new SelectedAgentLiveConnectionState(agentId: null, isConnected: false, isSubscribed: false, statusText: "Idle"));

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
