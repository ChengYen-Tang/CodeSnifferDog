using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

/// <summary>
/// Applies live-update DTOs to snapshot, selection, and history state.
/// </summary>
internal sealed class LiveUpdateReducer
{
    /// <summary>
    /// Applies one live update and synchronizes dependent selection/history state when needed.
    /// </summary>
    /// <param name="update">Live update to apply.</param>
    /// <param name="snapshot">Snapshot state to mutate.</param>
    /// <param name="selection">Selection state that may need reconciliation.</param>
    /// <param name="history">History state for the selected agent.</param>
    /// <returns><see langword="true" /> when the update changed state.</returns>
    public bool Apply(
        LiveUpdateDto update,
        SnapshotState snapshot,
        SelectionState selection,
        HistoryState history)
    {
        if (update.Kind == LiveUpdateKind.TimelineEntriesRemoved)
        {
            TimelineEntriesRemovedDto? removedEntries = update.RemovedTimelineEntries;
            if (removedEntries is null)
                return false;

            TimelineMutationResult? result =
                snapshot.RemoveTimelineEntries(removedEntries.AgentId, removedEntries.TimelineEntryIds);
            if (result is null)
                return false;

            if (removedEntries.AgentId == selection.SelectedAgentId)
            {
                history.ApplySelectedAgentSnapshot(
                    result.TimelineEntries,
                    removedEntries.AgentId,
                    result.LatestSequence);
            }

            return true;
        }

        if (update.Kind == LiveUpdateKind.TimelineEntryUpserted)
        {
            TimelineEntryDto? timelineEntry = update.TimelineEntry;
            if (timelineEntry is not null && timelineEntry.AgentId == selection.SelectedAgentId)
            {
                TimelineMutationResult? result =
                    snapshot.UpsertTimelineEntry(timelineEntry, history.GetLatestSequence());
                if (result is null)
                    return false;

                history.ApplySelectedAgentSnapshot(
                    result.TimelineEntries,
                    timelineEntry.AgentId,
                    result.LatestSequence);
                return true;
            }

            return false;
        }

        if (!snapshot.ApplyLiveUpdate(update))
            return false;

        selection.ApplySnapshot(snapshot);
        return true;
    }
}
