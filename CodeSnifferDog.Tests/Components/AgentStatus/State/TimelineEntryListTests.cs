using CodeSnifferDog.Server.Client.Components.AgentStatus.State;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeSnifferDog.Tests.Components.AgentStatus.State;

[TestClass]
public sealed class TimelineEntryListTests
{
    private static readonly Guid AgentId = Guid.Parse("72000000-0000-0000-0000-000000000001");

    [TestMethod]
    public void UpsertInsertsEntryBySequenceAndOccurredAt()
    {
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
        [
            CreateEntry(1, 10),
            CreateEntry(3, 30),
        ];

        IReadOnlyList<ProjectAgentTimelineEntryDto> result =
            TimelineEntryList.Upsert(timelineEntries, CreateEntry(2, 20));

        CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, result.Select(entry => entry.Sequence).ToArray());
    }

    [TestMethod]
    public void UpsertWithLatestSequenceReturnsUpdatedTimelineAndLatestSequence()
    {
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
        [
            CreateEntry(1, 10),
            CreateEntry(3, 30),
        ];

        TimelineMutationResult result =
            TimelineEntryList.UpsertWithLatestSequence(timelineEntries, CreateEntry(2, 20));

        CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, result.TimelineEntries.Select(entry => entry.Sequence).ToArray());
        Assert.AreEqual(3, result.LatestSequence);
    }

    [TestMethod]
    public void UpsertWithLatestSequenceUsesProvidedLatestSequence()
    {
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
        [
            CreateEntry(1, 10),
            CreateEntry(3, 30),
        ];

        TimelineMutationResult result =
            TimelineEntryList.UpsertWithLatestSequence(timelineEntries, CreateEntry(2, 20), latestSequence: 9);

        CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, result.TimelineEntries.Select(entry => entry.Sequence).ToArray());
        Assert.AreEqual(9, result.LatestSequence);
    }

    [TestMethod]
    public void UpsertReplacesExistingEntryAndRepositionsWhenOrderChanges()
    {
        Guid replacedEntryId = Guid.Parse("73000000-0000-0000-0000-000000000010");
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
        [
            CreateEntry(1, 10),
            CreateEntry(2, 20, replacedEntryId, "old"),
            CreateEntry(3, 30),
        ];

        IReadOnlyList<ProjectAgentTimelineEntryDto> result =
            TimelineEntryList.Upsert(timelineEntries, CreateEntry(4, 40, replacedEntryId, "new"));

        CollectionAssert.AreEqual(new long[] { 1, 3, 4 }, result.Select(entry => entry.Sequence).ToArray());
        Assert.AreEqual("new", result[^1].Message);
        Assert.AreEqual(replacedEntryId, result[^1].TimelineEntryId);
    }

    [TestMethod]
    public void UpsertPreservesExistingPositionWhenOrderKeyDoesNotChange()
    {
        Guid replacedEntryId = Guid.Parse("73000000-0000-0000-0000-000000000020");
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
        [
            CreateEntry(1, 10),
            CreateEntry(2, 20, replacedEntryId, "old"),
            CreateEntry(2, 20, Guid.Parse("73000000-0000-0000-0000-000000000021"), "same key"),
        ];

        IReadOnlyList<ProjectAgentTimelineEntryDto> result =
            TimelineEntryList.Upsert(timelineEntries, CreateEntry(2, 20, replacedEntryId, "new"));

        CollectionAssert.AreEqual(
            new[] { "entry-1", "new", "same key" },
            result.Select(entry => entry.Message).ToArray());
    }

    [TestMethod]
    public void UpsertOrdersDuplicateSequenceByOccurredAt()
    {
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
        [
            CreateEntry(1, 10),
            CreateEntry(2, 30, Guid.Parse("73000000-0000-0000-0000-000000000201"), "late"),
            CreateEntry(3, 40),
        ];

        IReadOnlyList<ProjectAgentTimelineEntryDto> result =
            TimelineEntryList.Upsert(
                timelineEntries,
                CreateEntry(2, 20, Guid.Parse("73000000-0000-0000-0000-000000000202"), "early"));

        CollectionAssert.AreEqual(
            new[] { "entry-1", "early", "late", "entry-3" },
            result.Select(entry => entry.Message).ToArray());
    }

    [TestMethod]
    public void UpsertNormalizesUnsortedExistingTimeline()
    {
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
        [
            CreateEntry(3, 30),
            CreateEntry(1, 10),
        ];

        IReadOnlyList<ProjectAgentTimelineEntryDto> result =
            TimelineEntryList.Upsert(timelineEntries, CreateEntry(2, 20));

        CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, result.Select(entry => entry.Sequence).ToArray());
    }

    [TestMethod]
    public void RemoveReturnsRemainingEntriesAndPreservesNoOpShape()
    {
        Guid removedEntryId = Guid.Parse("73000000-0000-0000-0000-000000000030");
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
        [
            CreateEntry(1, 10),
            CreateEntry(2, 20, removedEntryId),
            CreateEntry(3, 30),
        ];

        IReadOnlyList<ProjectAgentTimelineEntryDto> removed =
            TimelineEntryList.Remove(timelineEntries, new HashSet<Guid> { removedEntryId });
        IReadOnlyList<ProjectAgentTimelineEntryDto> noOp =
            TimelineEntryList.Remove(timelineEntries, new HashSet<Guid> { Guid.Parse("73000000-0000-0000-0000-000000000099") });

        CollectionAssert.AreEqual(new long[] { 1, 3 }, removed.Select(entry => entry.Sequence).ToArray());
        CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, noOp.Select(entry => entry.Sequence).ToArray());
    }

    [TestMethod]
    public void RemoveWithLatestSequenceReturnsRemainingEntriesAndLatestSequence()
    {
        Guid removedEntryId = Guid.Parse("73000000-0000-0000-0000-000000000031");
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
        [
            CreateEntry(1, 10),
            CreateEntry(2, 20),
            CreateEntry(5, 50, removedEntryId),
        ];

        TimelineMutationResult? result =
            TimelineEntryList.RemoveWithLatestSequence(timelineEntries, new HashSet<Guid> { removedEntryId });

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(new long[] { 1, 2 }, result.TimelineEntries.Select(entry => entry.Sequence).ToArray());
        Assert.AreEqual(2, result.LatestSequence);
    }

    [TestMethod]
    public void RemoveWithLatestSequenceReturnsNullWhenNoEntriesAreRemoved()
    {
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
        [
            CreateEntry(1, 10),
            CreateEntry(2, 20),
        ];

        TimelineMutationResult? result =
            TimelineEntryList.RemoveWithLatestSequence(
                timelineEntries,
                new HashSet<Guid> { Guid.Parse("73000000-0000-0000-0000-000000000099") });

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetLatestSequenceTracksAddReplaceAndRemoveResults()
    {
        Guid replacedEntryId = Guid.Parse("73000000-0000-0000-0000-000000000040");
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
        [
            CreateEntry(1, 10),
            CreateEntry(5, 50, replacedEntryId),
        ];

        Assert.AreEqual(5, TimelineEntryList.GetLatestSequence(timelineEntries));

        IReadOnlyList<ProjectAgentTimelineEntryDto> lowered =
            TimelineEntryList.Upsert(timelineEntries, CreateEntry(2, 20, replacedEntryId));
        Assert.AreEqual(2, TimelineEntryList.GetLatestSequence(lowered));

        IReadOnlyList<ProjectAgentTimelineEntryDto> raised =
            TimelineEntryList.Upsert(lowered, CreateEntry(9, 90));
        Assert.AreEqual(9, TimelineEntryList.GetLatestSequence(raised));

        IReadOnlyList<ProjectAgentTimelineEntryDto> removed =
            TimelineEntryList.Remove(raised, new HashSet<Guid>(raised.Select(entry => entry.TimelineEntryId)));
        Assert.AreEqual(0, TimelineEntryList.GetLatestSequence(removed));
    }

    [TestMethod]
    public void LargeTimelineUpsertAndRemoveKeepExpectedStructure()
    {
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries = Enumerable.Range(1, 500)
            .Select(index => CreateEntry(index * 2, index * 2))
            .ToList();

        IReadOnlyList<ProjectAgentTimelineEntryDto> withInserted =
            TimelineEntryList.Upsert(timelineEntries, CreateEntry(501, 501));
        IReadOnlyList<ProjectAgentTimelineEntryDto> withRemoved =
            TimelineEntryList.Remove(withInserted, new HashSet<Guid>
            {
                withInserted[0].TimelineEntryId,
                withInserted[^1].TimelineEntryId,
            });

        Assert.HasCount(501, withInserted);
        Assert.AreEqual(500, withInserted[249].Sequence);
        Assert.AreEqual(501, withInserted[250].Sequence);
        Assert.AreEqual(502, withInserted[251].Sequence);
        Assert.HasCount(499, withRemoved);
        Assert.AreEqual(4, withRemoved[0].Sequence);
        Assert.AreEqual(998, withRemoved[^1].Sequence);
    }

    private static ProjectAgentTimelineEntryDto CreateEntry(
        long sequence,
        int occurredSecond,
        Guid? timelineEntryId = null,
        string? message = null) => new()
        {
            TimelineEntryId = timelineEntryId ?? Guid.Parse($"73000000-0000-0000-0000-{sequence:000000000000}"),
            AgentId = AgentId,
            Sequence = sequence,
            EntryKind = ProjectAgentTimelineEntryKind.Output,
            OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 10, 0, occurredSecond % 60, TimeSpan.Zero),
            Message = message ?? $"entry-{sequence}",
        };
}
