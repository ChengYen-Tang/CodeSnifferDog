using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal static class AgentStatusTimelineEntries
{
    public static IReadOnlyList<ProjectAgentTimelineEntryDto> Upsert(
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries,
        ProjectAgentTimelineEntryDto timelineEntry)
    {
        List<ProjectAgentTimelineEntryDto> nextTimelineEntries = timelineEntries.ToList();
        bool isSorted = IsSorted(timelineEntries);
        int existingIndex = nextTimelineEntries.FindIndex(candidate => candidate.TimelineEntryId == timelineEntry.TimelineEntryId);
        if (existingIndex >= 0)
        {
            ProjectAgentTimelineEntryDto existingEntry = nextTimelineEntries[existingIndex];
            if (isSorted && CompareOrder(existingEntry, timelineEntry) == 0)
            {
                nextTimelineEntries[existingIndex] = timelineEntry;
                return nextTimelineEntries;
            }

            nextTimelineEntries.RemoveAt(existingIndex);
        }

        if (!isSorted)
        {
            nextTimelineEntries.Add(timelineEntry);
            return Normalize(nextTimelineEntries);
        }

        nextTimelineEntries.Insert(GetInsertIndex(nextTimelineEntries, timelineEntry), timelineEntry);
        return nextTimelineEntries;
    }

    public static IReadOnlyList<ProjectAgentTimelineEntryDto> Remove(
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries,
        IReadOnlySet<Guid> timelineEntryIds)
    {
        List<ProjectAgentTimelineEntryDto> nextTimelineEntries = new(timelineEntries.Count);
        for (int index = 0; index < timelineEntries.Count; index++)
        {
            ProjectAgentTimelineEntryDto entry = timelineEntries[index];
            if (!timelineEntryIds.Contains(entry.TimelineEntryId))
                nextTimelineEntries.Add(entry);
        }

        return nextTimelineEntries;
    }

    public static IReadOnlyList<ProjectAgentTimelineEntryDto> Normalize(
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries) =>
        timelineEntries
            .OrderBy(candidate => candidate.Sequence)
            .ThenBy(candidate => candidate.OccurredAtUtc)
            .ToList();

    public static long GetLatestSequence(IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries)
    {
        long latestSequence = 0;
        for (int index = 0; index < timelineEntries.Count; index++)
        {
            if (timelineEntries[index].Sequence > latestSequence)
                latestSequence = timelineEntries[index].Sequence;
        }

        return latestSequence;
    }

    private static int GetInsertIndex(
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries,
        ProjectAgentTimelineEntryDto timelineEntry)
    {
        int low = 0;
        int high = timelineEntries.Count;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (CompareOrder(timelineEntries[middle], timelineEntry) <= 0)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }

    private static bool IsSorted(IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries)
    {
        for (int index = 1; index < timelineEntries.Count; index++)
        {
            if (CompareOrder(timelineEntries[index - 1], timelineEntries[index]) > 0)
                return false;
        }

        return true;
    }

    private static int CompareOrder(ProjectAgentTimelineEntryDto left, ProjectAgentTimelineEntryDto right)
    {
        int sequenceComparison = left.Sequence.CompareTo(right.Sequence);
        return sequenceComparison != 0
            ? sequenceComparison
            : left.OccurredAtUtc.CompareTo(right.OccurredAtUtc);
    }
}
