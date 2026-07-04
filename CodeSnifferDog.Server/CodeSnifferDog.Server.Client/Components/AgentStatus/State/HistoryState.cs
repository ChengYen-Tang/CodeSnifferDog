using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

/// <summary>
/// Tracks the selected agent's history panel state, including loaded entries, loading state, and latest seen sequence.
/// </summary>
/// <param name="agentId">Initially selected agent identifier.</param>
/// <param name="timelineEntries">Initial history entries for the selected agent.</param>
/// <param name="isLoading">Initial loading flag.</param>
internal sealed class HistoryState(Guid? agentId, IReadOnlyList<TimelineEntryDto> timelineEntries, bool isLoading)
{
    private long _latestSequence = TimelineEntryList.GetLatestSequence(timelineEntries);

    /// <summary>
    /// Gets the agent whose history is currently shown, if any.
    /// </summary>
    public Guid? AgentId { get; private set; } = agentId;

    /// <summary>
    /// Gets the currently displayed timeline entries.
    /// </summary>
    public IReadOnlyList<TimelineEntryDto> TimelineEntries { get; private set; } = timelineEntries;

    /// <summary>
    /// Gets whether history loading is in progress.
    /// </summary>
    public bool IsLoading { get; private set; } = isLoading;

    /// <summary>
    /// Gets the latest history-load error message, if one exists.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Reconciles history state against a replacement snapshot and the current selection.
    /// </summary>
    /// <param name="snapshot">Snapshot state that may provide replacement history entries.</param>
    /// <param name="selectedAgentId">Currently selected agent identifier.</param>
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

    /// <summary>
    /// Replaces the selected agent's history and recomputes the latest sequence.
    /// </summary>
    /// <param name="timelineEntries">Replacement timeline entries.</param>
    /// <param name="agentId">Selected agent identifier.</param>
    public void ApplySelectedAgentSnapshot(IReadOnlyList<TimelineEntryDto> timelineEntries, Guid agentId)
    {
        ApplySelectedAgentSnapshot(
            timelineEntries,
            agentId,
            TimelineEntryList.GetLatestSequence(timelineEntries));
    }

    /// <summary>
    /// Replaces the selected agent's history using an already computed latest sequence.
    /// </summary>
    /// <param name="timelineEntries">Replacement timeline entries.</param>
    /// <param name="agentId">Selected agent identifier.</param>
    /// <param name="latestSequence">Latest sequence already known for the replacement entries.</param>
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

    /// <summary>
    /// Switches the history panel into a loading state for the supplied agent.
    /// </summary>
    /// <param name="agentId">Agent identifier whose history is being loaded.</param>
    public void StartLoading(Guid agentId)
    {
        AgentId = agentId;
        TimelineEntries = [];
        _latestSequence = 0;
        IsLoading = true;
        ErrorMessage = null;
    }

    /// <summary>
    /// Ends the loading state when the completed request still matches the current agent.
    /// </summary>
    /// <param name="agentId">Agent identifier whose load completed.</param>
    public void FinishLoading(Guid agentId)
    {
        if (AgentId == agentId)
            IsLoading = false;
    }

    /// <summary>
    /// Stores a history-load error for the supplied agent and clears the current entries.
    /// </summary>
    /// <param name="agentId">Agent identifier associated with the error.</param>
    /// <param name="errorMessage">Error message to store.</param>
    public void SetError(Guid agentId, string errorMessage)
    {
        AgentId = agentId;
        TimelineEntries = [];
        _latestSequence = 0;
        IsLoading = false;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Clears the current history-load error message.
    /// </summary>
    public void ClearError()
    {
        ErrorMessage = null;
    }

    /// <summary>
    /// Gets the latest sequence number currently known for the displayed history.
    /// </summary>
    /// <returns>The latest known sequence number.</returns>
    public long GetLatestSequence() => _latestSequence;
}
