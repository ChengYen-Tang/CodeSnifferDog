using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

/// <summary>
/// Provides immutable-style timeline mutations while preserving ordering and latest-sequence tracking.
/// </summary>
internal static class TimelineEntryList
{
    /// <summary>
    /// Inserts or replaces one timeline entry and returns only the rewritten list.
    /// </summary>
    /// <param name="timelineEntries">Existing timeline entries.</param>
    /// <param name="timelineEntry">Entry to insert or replace.</param>
    /// <returns>The rewritten timeline entries.</returns>
    public static IReadOnlyList<TimelineEntryDto> Upsert(
        IReadOnlyList<TimelineEntryDto> timelineEntries,
        TimelineEntryDto timelineEntry) =>
        UpsertWithLatestSequence(
            timelineEntries,
            timelineEntry,
            GetLatestSequence(timelineEntries)).TimelineEntries;

    /// <summary>
    /// Inserts or replaces one timeline entry and recomputes the latest sequence from the current list.
    /// </summary>
    /// <param name="timelineEntries">Existing timeline entries.</param>
    /// <param name="timelineEntry">Entry to insert or replace.</param>
    /// <returns>The rewritten timeline entries and latest sequence.</returns>
    public static TimelineMutationResult UpsertWithLatestSequence(
        IReadOnlyList<TimelineEntryDto> timelineEntries,
        TimelineEntryDto timelineEntry) =>
        UpsertWithLatestSequence(
            timelineEntries,
            timelineEntry,
            GetLatestSequence(timelineEntries));

    /// <summary>
    /// Inserts or replaces one timeline entry using a caller-provided latest sequence baseline.
    /// </summary>
    /// <param name="timelineEntries">Existing timeline entries.</param>
    /// <param name="timelineEntry">Entry to insert or replace.</param>
    /// <param name="latestSequence">Latest sequence already known for <paramref name="timelineEntries" />.</param>
    /// <returns>The rewritten timeline entries and updated latest sequence.</returns>
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

    /// <summary>
    /// Removes timeline entries by identifier and returns only the rewritten list.
    /// </summary>
    /// <param name="timelineEntries">Existing timeline entries.</param>
    /// <param name="timelineEntryIds">Identifiers of entries to remove.</param>
    /// <returns>The rewritten timeline entries, or the original list when nothing was removed.</returns>
    public static IReadOnlyList<TimelineEntryDto> Remove(
        IReadOnlyList<TimelineEntryDto> timelineEntries,
        IReadOnlySet<Guid> timelineEntryIds) =>
        RemoveWithLatestSequence(timelineEntries, timelineEntryIds)?.TimelineEntries ?? timelineEntries;

    /// <summary>
    /// Removes timeline entries by identifier and returns the updated latest sequence when a mutation occurred.
    /// </summary>
    /// <param name="timelineEntries">Existing timeline entries.</param>
    /// <param name="timelineEntryIds">Identifiers of entries to remove.</param>
    /// <returns>The rewritten timeline entries and latest sequence, or <see langword="null" /> when nothing changed.</returns>
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

    /// <summary>
    /// Returns a normalized copy of the timeline entries ordered by sequence and occurrence time.
    /// </summary>
    /// <param name="timelineEntries">Timeline entries to normalize.</param>
    /// <returns>The normalized timeline entries.</returns>
    public static IReadOnlyList<TimelineEntryDto> Normalize(
        IReadOnlyList<TimelineEntryDto> timelineEntries) =>
        timelineEntries
            .OrderBy(candidate => candidate.Sequence)
            .ThenBy(candidate => candidate.OccurredAtUtc)
            .ToList();

    /// <summary>
    /// Gets the highest sequence number present in the timeline.
    /// </summary>
    /// <param name="timelineEntries">Timeline entries to inspect.</param>
    /// <returns>The highest sequence number, or zero when the timeline is empty.</returns>
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

    /// <summary>
    /// Finds the insertion index that preserves timeline ordering.
    /// </summary>
    /// <param name="timelineEntries">Ordered timeline entries.</param>
    /// <param name="timelineEntry">Entry to insert.</param>
    /// <returns>The insertion index for <paramref name="timelineEntry" />.</returns>
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

    /// <summary>
    /// Determines whether the supplied timeline is already sorted by this helper's comparison rules.
    /// </summary>
    /// <param name="timelineEntries">Timeline entries to inspect.</param>
    /// <returns><see langword="true" /> when the timeline is already sorted.</returns>
    private static bool IsSorted(IReadOnlyList<TimelineEntryDto> timelineEntries)
    {
        for (int index = 1; index < timelineEntries.Count; index++)
        {
            if (CompareOrder(timelineEntries[index - 1], timelineEntries[index]) > 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Compares two timeline entries by sequence and then occurrence time.
    /// </summary>
    /// <param name="left">First entry.</param>
    /// <param name="right">Second entry.</param>
    /// <returns>A comparison value suitable for sorting and binary insertion.</returns>
    private static int CompareOrder(TimelineEntryDto left, TimelineEntryDto right)
    {
        int sequenceComparison = left.Sequence.CompareTo(right.Sequence);
        return sequenceComparison != 0
            ? sequenceComparison
            : left.OccurredAtUtc.CompareTo(right.OccurredAtUtc);
    }
}
