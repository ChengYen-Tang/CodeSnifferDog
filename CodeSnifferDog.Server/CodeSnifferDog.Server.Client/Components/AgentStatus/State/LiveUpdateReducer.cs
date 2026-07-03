using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed class LiveUpdateReducer
{
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
