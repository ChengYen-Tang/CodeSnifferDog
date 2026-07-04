using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

/// <summary>
/// Holds the client-side agent-status page state, including snapshot data, selection, history, and live connection status.
/// </summary>
/// <param name="snapshot">Snapshot state backing the current page view.</param>
/// <param name="liveConnection">Overall live connection state for the page.</param>
/// <param name="selection">Current agent selection and expanded tool-detail state.</param>
/// <param name="history">History pane state for the selected agent.</param>
/// <param name="selectedAgentLiveConnection">Live connection state scoped to the currently selected agent.</param>
internal sealed class PageState(
    SnapshotState snapshot,
    LiveConnectionState liveConnection,
    SelectionState selection,
    HistoryState history,
    SelectedAgentLiveConnectionState selectedAgentLiveConnection)
{
    /// <summary>
    /// Applies live updates to snapshot, selection, and history state as one coordinated operation.
    /// </summary>
    private readonly LiveUpdateReducer _liveUpdateReducer = new();

    /// <summary>
    /// Gets the current snapshot state.
    /// </summary>
    public SnapshotState Snapshot { get; } = snapshot;

    /// <summary>
    /// Gets the overall live connection state for the page.
    /// </summary>
    public LiveConnectionState LiveConnection { get; } = liveConnection;

    /// <summary>
    /// Gets the current selection state.
    /// </summary>
    public SelectionState Selection { get; } = selection;

    /// <summary>
    /// Gets the history pane state for the selected agent.
    /// </summary>
    public HistoryState History { get; } = history;

    /// <summary>
    /// Gets the live connection state scoped to the selected agent.
    /// </summary>
    public SelectedAgentLiveConnectionState SelectedAgentLiveConnection { get; } = selectedAgentLiveConnection;

    /// <summary>
    /// Gets whether the initial snapshot is currently loading.
    /// </summary>
    public bool IsSnapshotLoading { get; private set; }

    /// <summary>
    /// Gets the latest snapshot-load error message, when one exists.
    /// </summary>
    public string? SnapshotLoadErrorMessage { get; private set; }

    /// <summary>
    /// Gets the latest live-connection error message, when one exists.
    /// </summary>
    public string? LiveConnectionErrorMessage { get; private set; }

    /// <summary>
    /// Gets the derived snapshot-load status text shown by the UI.
    /// </summary>
    public string SnapshotLoadStatusText =>
        SnapshotLoadErrorMessage is not null
            ? "Snapshot unavailable"
            : IsSnapshotLoading
                ? "Snapshot loading"
                : Snapshot.Snapshot is not null
                    ? "Snapshot loaded"
                : "Idle";

    /// <summary>
    /// Gets the derived completion badge state shown by the UI.
    /// </summary>
    public CompletionState Completion =>
        CompletionState.From(Snapshot.Snapshot?.ProjectStatus, LiveConnection);

    /// <summary>
    /// Creates an empty page state suitable for the first render.
    /// </summary>
    /// <returns>An empty page state.</returns>
    public static PageState CreateEmpty() =>
        new(
            new SnapshotState(null),
            new LiveConnectionState(isConnected: false, isSubscribed: false, statusText: "Idle"),
            new SelectionState(selectedAgentId: null, expandedToolDetails: []),
            new HistoryState(agentId: null, timelineEntries: [], isLoading: false),
            new SelectedAgentLiveConnectionState(agentId: null, isConnected: false, isSubscribed: false, statusText: "Idle"));

    /// <summary>
    /// Replaces the snapshot and realigns selection, history, and retained per-agent history state.
    /// </summary>
    /// <param name="snapshot">Replacement snapshot payload.</param>
    public void SetSnapshot(StatusSnapshotDto? snapshot)
    {
        Snapshot.Replace(snapshot);
        Selection.ApplySnapshot(Snapshot);
        History.ApplySnapshot(Snapshot, Selection.SelectedAgentId);
        Snapshot.ReleaseHistoryExcept(Selection.SelectedAgentId);
    }

    /// <summary>
    /// Updates the overall live connection state.
    /// </summary>
    /// <param name="isConnected">Whether the client is connected to the live-update transport.</param>
    /// <param name="isSubscribed">Whether the client is subscribed to live updates.</param>
    /// <param name="statusText">UI status text describing the current connection state.</param>
    public void SetLiveConnection(bool isConnected, bool isSubscribed, string statusText)
    {
        LiveConnection.Update(isConnected, isSubscribed, statusText);
    }

    /// <summary>
    /// Updates the live connection state associated with the currently selected agent.
    /// </summary>
    /// <param name="agentId">Selected agent identifier associated with the live connection state.</param>
    /// <param name="isConnected">Whether the selected-agent transport is connected.</param>
    /// <param name="isSubscribed">Whether the selected-agent stream is subscribed.</param>
    /// <param name="statusText">UI status text describing the selected-agent connection state.</param>
    public void SetSelectedAgentLiveConnection(Guid? agentId, bool isConnected, bool isSubscribed, string statusText)
    {
        SelectedAgentLiveConnection.Update(agentId, isConnected, isSubscribed, statusText);
    }

    /// <summary>
    /// Stores the latest live-connection error message.
    /// </summary>
    /// <param name="errorMessage">Error message to store, or <see langword="null" /> to clear it.</param>
    public void SetLiveConnectionError(string? errorMessage)
    {
        LiveConnectionErrorMessage = errorMessage;
    }

    /// <summary>
    /// Updates snapshot loading state and the latest snapshot-load error message.
    /// </summary>
    /// <param name="isLoading">Whether snapshot loading is currently in progress.</param>
    /// <param name="errorMessage">Latest snapshot-load error message, or <see langword="null" /> when no error exists.</param>
    public void SetSnapshotLoadState(bool isLoading, string? errorMessage)
    {
        IsSnapshotLoading = isLoading;
        SnapshotLoadErrorMessage = errorMessage;
    }

    /// <summary>
    /// Applies one live update to snapshot, selection, and history state.
    /// </summary>
    /// <param name="update">Live update to apply.</param>
    /// <returns><see langword="true" /> when the update changed any state.</returns>
    public bool ApplyLiveUpdate(LiveUpdateDto update) =>
        _liveUpdateReducer.Apply(update, Snapshot, Selection, History);

    /// <summary>
    /// Replaces the cached timeline history for one agent and updates the history pane when that agent is selected.
    /// </summary>
    /// <param name="agentId">Agent identifier whose timeline history should be replaced.</param>
    /// <param name="timelineEntries">Replacement timeline entries.</param>
    public void SetAgentHistory(Guid agentId, IReadOnlyList<TimelineEntryDto> timelineEntries)
    {
        Snapshot.ReplaceAgentHistory(agentId, timelineEntries);
        History.ApplySelectedAgentSnapshot(timelineEntries, agentId);
    }

    /// <summary>
    /// Selects one agent, releases other agents' cached history, and resets selected-agent connection state to switching.
    /// </summary>
    /// <param name="agentId">Agent identifier to select.</param>
    public void SelectAgent(Guid agentId)
    {
        Selection.SelectAgent(agentId);
        Snapshot.ReleaseHistoryExcept(agentId);
        SelectedAgentLiveConnection.Update(agentId, isConnected: false, isSubscribed: false, statusText: "Switching");
    }
}
