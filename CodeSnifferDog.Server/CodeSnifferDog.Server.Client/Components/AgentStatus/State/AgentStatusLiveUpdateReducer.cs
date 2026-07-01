using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed class AgentStatusLiveUpdateReducer
{
    public bool Apply(
        ProjectAgentLiveUpdateDto update,
        AgentStatusSnapshotState snapshot,
        AgentStatusSelectionState selection,
        AgentStatusHistoryState history)
    {
        if (update.Kind == ProjectAgentLiveUpdateKind.TimelineEntriesRemoved)
        {
            ProjectAgentTimelineEntriesRemovedDto? removedEntries = update.RemovedTimelineEntries;
            if (removedEntries is null)
                return false;

            if (!snapshot.RemoveTimelineEntries(removedEntries.AgentId, removedEntries.TimelineEntryIds))
                return false;

            if (removedEntries.AgentId == selection.SelectedAgentId)
                history.ApplySelectedAgentSnapshot(snapshot.GetHistory(removedEntries.AgentId), removedEntries.AgentId);

            return true;
        }

        if (update.Kind == ProjectAgentLiveUpdateKind.TimelineEntryUpserted)
        {
            ProjectAgentTimelineEntryDto? timelineEntry = update.TimelineEntry;
            if (timelineEntry is not null && timelineEntry.AgentId == selection.SelectedAgentId)
            {
                if (!snapshot.UpsertTimelineEntry(timelineEntry))
                    return false;

                history.ApplySelectedAgentSnapshot(snapshot.GetHistory(timelineEntry.AgentId), timelineEntry.AgentId);
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
