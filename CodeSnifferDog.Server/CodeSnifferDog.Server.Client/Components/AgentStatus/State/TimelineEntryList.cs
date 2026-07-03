using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal static class TimelineEntryList
{
    public static IReadOnlyList<TimelineEntryDto> Upsert(
        IReadOnlyList<TimelineEntryDto> timelineEntries,
        TimelineEntryDto timelineEntry) =>
        UpsertWithLatestSequence(
            timelineEntries,
            timelineEntry,
            GetLatestSequence(timelineEntries)).TimelineEntries;

    public static TimelineMutationResult UpsertWithLatestSequence(
        IReadOnlyList<TimelineEntryDto> timelineEntries,
        TimelineEntryDto timelineEntry) =>
        UpsertWithLatestSequence(
            timelineEntries,
            timelineEntry,
            GetLatestSequence(timelineEntries));

    public static TimelineMutationResult UpsertWithLatestSequence(
        IReadOnlyList<TimelineEntryDto> timelineEntries,
        TimelineEntryDto timelineEntry,
        long latestSequence)
    {
        List<TimelineEntryDto> nextTimelineEntries = timelineEntries.ToList();
        bool isSorted = IsSorted(timelineEntries);
        int existingIndex = nextTimelineEntries.FindIndex(candidate => candidate.TimelineEntryId == timelineEntry.TimelineEntryId);
        if (existingIndex >= 0)
        {
            TimelineEntryDto existingEntry = nextTimelineEntries[existingIndex];
            if (isSorted && CompareOrder(existingEntry, timelineEntry) == 0)
            {
                nextTimelineEntries[existingIndex] = timelineEntry;
                return new TimelineMutationResult(
                    nextTimelineEntries,
                    Math.Max(latestSequence, timelineEntry.Sequence));
            }

            nextTimelineEntries.RemoveAt(existingIndex);
            if (existingEntry.Sequence == latestSequence && timelineEntry.Sequence < latestSequence)
                latestSequence = GetLatestSequence(nextTimelineEntries);
        }

        if (!isSorted)
        {
            nextTimelineEntries.Add(timelineEntry);
            IReadOnlyList<TimelineEntryDto> normalizedTimelineEntries = Normalize(nextTimelineEntries);
            return new TimelineMutationResult(
                normalizedTimelineEntries,
                GetLatestSequence(normalizedTimelineEntries));
        }

        nextTimelineEntries.Insert(GetInsertIndex(nextTimelineEntries, timelineEntry), timelineEntry);
        return new TimelineMutationResult(
            nextTimelineEntries,
            Math.Max(latestSequence, timelineEntry.Sequence));
    }

    public static IReadOnlyList<TimelineEntryDto> Remove(
        IReadOnlyList<TimelineEntryDto> timelineEntries,
        IReadOnlySet<Guid> timelineEntryIds) =>
        RemoveWithLatestSequence(timelineEntries, timelineEntryIds)?.TimelineEntries ?? timelineEntries;

    public static TimelineMutationResult? RemoveWithLatestSequence(
        IReadOnlyList<TimelineEntryDto> timelineEntries,
        IReadOnlySet<Guid> timelineEntryIds)
    {
        List<TimelineEntryDto> nextTimelineEntries = new(timelineEntries.Count);
        long latestSequence = 0;
        bool removedAny = false;
        for (int index = 0; index < timelineEntries.Count; index++)
        {
            TimelineEntryDto entry = timelineEntries[index];
            if (timelineEntryIds.Contains(entry.TimelineEntryId))
            {
                removedAny = true;
                continue;
            }

            nextTimelineEntries.Add(entry);
            if (entry.Sequence > latestSequence)
                latestSequence = entry.Sequence;
        }

        return removedAny
            ? new TimelineMutationResult(nextTimelineEntries, latestSequence)
            : null;
    }

    public static IReadOnlyList<TimelineEntryDto> Normalize(
        IReadOnlyList<TimelineEntryDto> timelineEntries) =>
        timelineEntries
            .OrderBy(candidate => candidate.Sequence)
            .ThenBy(candidate => candidate.OccurredAtUtc)
            .ToList();

    public static long GetLatestSequence(IReadOnlyList<TimelineEntryDto> timelineEntries)
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
        IReadOnlyList<TimelineEntryDto> timelineEntries,
        TimelineEntryDto timelineEntry)
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

    private static bool IsSorted(IReadOnlyList<TimelineEntryDto> timelineEntries)
    {
        for (int index = 1; index < timelineEntries.Count; index++)
        {
            if (CompareOrder(timelineEntries[index - 1], timelineEntries[index]) > 0)
                return false;
        }

        return true;
    }

    private static int CompareOrder(TimelineEntryDto left, TimelineEntryDto right)
    {
        int sequenceComparison = left.Sequence.CompareTo(right.Sequence);
        return sequenceComparison != 0
            ? sequenceComparison
            : left.OccurredAtUtc.CompareTo(right.OccurredAtUtc);
    }
}
