using CodeSnifferDog.Server.Client.Components.AgentStatus.State;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeSnifferDog.Tests.Components.AgentStatus.State;

[TestClass]
public sealed class AgentStatusPageStateTests
{
    [TestMethod]
    public void SetSnapshotSelectsFirstAgentPreservesExistingSelectionAndFallsBackWhenMissing()
    {
        AgentStatusPageState state = AgentStatusPageState.CreateEmpty();
        Guid firstAgentId = Guid.Parse("72000000-0000-0000-0000-000000000001");
        Guid secondAgentId = Guid.Parse("72000000-0000-0000-0000-000000000002");
        Guid replacementAgentId = Guid.Parse("72000000-0000-0000-0000-000000000003");

        state.SetSnapshot(CreateSnapshot(
            [
                CreateGroup(
                    Guid.Parse("71000000-0000-0000-0000-000000000001"),
                    "Group A",
                    1,
                    [
                        CreateAgent(firstAgentId, Guid.Parse("71000000-0000-0000-0000-000000000001"), "First", 1, []),
                        CreateAgent(secondAgentId, Guid.Parse("71000000-0000-0000-0000-000000000001"), "Second", 2, []),
                    ]),
            ]));

        Assert.AreEqual(firstAgentId, state.Selection.SelectedAgentId);

        state.SelectAgent(secondAgentId);
        state.Selection.ToggleToolDetails("tool-key");
        state.SetSnapshot(CreateSnapshot(
            [
                CreateGroup(
                    Guid.Parse("71000000-0000-0000-0000-000000000001"),
                    "Group A",
                    1,
                    [
                        CreateAgent(firstAgentId, Guid.Parse("71000000-0000-0000-0000-000000000001"), "First", 1, []),
                        CreateAgent(secondAgentId, Guid.Parse("71000000-0000-0000-0000-000000000001"), "Second", 2, []),
                    ]),
            ]));

        Assert.AreEqual(secondAgentId, state.Selection.SelectedAgentId);
        Assert.HasCount(1, state.Selection.ExpandedToolDetails);

        state.SetSnapshot(CreateSnapshot(
            [
                CreateGroup(
                    Guid.Parse("71000000-0000-0000-0000-000000000002"),
                    "Group B",
                    1,
                    [CreateAgent(replacementAgentId, Guid.Parse("71000000-0000-0000-0000-000000000002"), "Replacement", 1, [])]),
            ]));

        Assert.AreEqual(replacementAgentId, state.Selection.SelectedAgentId);
        Assert.IsEmpty(state.Selection.ExpandedToolDetails);
    }

    [TestMethod]
    public void SelectAgentClearsExpandedToolDetailsAndReleasesOtherAgentHistory()
    {
        AgentStatusPageState state = AgentStatusPageState.CreateEmpty();
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000010");
        Guid firstAgentId = Guid.Parse("72000000-0000-0000-0000-000000000010");
        Guid secondAgentId = Guid.Parse("72000000-0000-0000-0000-000000000011");
        state.SetSnapshot(CreateSnapshot(
            [
                CreateGroup(
                    groupId,
                    "Group A",
                    1,
                    [
                        CreateAgent(firstAgentId, groupId, "First", 1, [CreateTimelineEntry(firstAgentId, 1, "first")]),
                        CreateAgent(secondAgentId, groupId, "Second", 2, []),
                    ]),
            ]));
        state.SetAgentHistory(secondAgentId, [CreateTimelineEntry(secondAgentId, 1, "second")]);
        state.Selection.ToggleToolDetails("tool-key");

        state.SelectAgent(secondAgentId);

        Assert.AreEqual(secondAgentId, state.Selection.SelectedAgentId);
        Assert.IsEmpty(state.Selection.ExpandedToolDetails);
        Assert.IsFalse(state.Snapshot.FindAgent(firstAgentId)!.HasLoadedHistory);
        Assert.IsEmpty(state.Snapshot.FindAgent(firstAgentId)!.TimelineEntries);
        Assert.IsTrue(state.Snapshot.FindAgent(secondAgentId)!.HasLoadedHistory);
        Assert.HasCount(1, state.Snapshot.FindAgent(secondAgentId)!.TimelineEntries);
    }

    [TestMethod]
    public void ApplyLiveUpdateHandlesNoOpAndProjectStatusUpdates()
    {
        AgentStatusPageState state = AgentStatusPageState.CreateEmpty();
        state.SetSnapshot(CreateSnapshot([]));

        bool noOpChanged = state.ApplyLiveUpdate(new ProjectAgentLiveUpdateDto
        {
            ProjectId = Guid.Parse("70000000-0000-0000-0000-000000000001"),
            Kind = ProjectAgentLiveUpdateKind.AgentStatusChanged,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            AgentStatus = new ProjectAgentStatusChangedDto
            {
                AgentId = Guid.Parse("72000000-0000-0000-0000-000000000099"),
                Status = ProjectAgentRunStatus.Completed,
                OccurredAtUtc = DateTimeOffset.UtcNow,
            },
        });

        Assert.IsFalse(noOpChanged);

        bool projectChanged = state.ApplyLiveUpdate(new ProjectAgentLiveUpdateDto
        {
            ProjectId = Guid.Parse("70000000-0000-0000-0000-000000000001"),
            Kind = ProjectAgentLiveUpdateKind.ProjectStatusChanged,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ProjectStatus = new ProjectExecutionStatusChangedDto
            {
                Status = ProjectStatus.Failed,
            },
        });

        Assert.IsTrue(projectChanged);
        Assert.AreEqual(ProjectStatus.Failed, state.Snapshot.Snapshot!.ProjectStatus);
    }

    [TestMethod]
    public void ApplyLiveUpdateMaintainsGroupAgentAndTimelineOrdering()
    {
        AgentStatusPageState state = AgentStatusPageState.CreateEmpty();
        Guid earlyGroupId = Guid.Parse("71000000-0000-0000-0000-000000000020");
        Guid lateGroupId = Guid.Parse("71000000-0000-0000-0000-000000000021");
        Guid earlyAgentId = Guid.Parse("72000000-0000-0000-0000-000000000020");
        Guid lateAgentId = Guid.Parse("72000000-0000-0000-0000-000000000021");
        state.SetSnapshot(CreateSnapshot(
            [
                CreateGroup(lateGroupId, "Late Group", 2, []),
            ]));

        state.ApplyLiveUpdate(new ProjectAgentLiveUpdateDto
        {
            ProjectId = Guid.Parse("70000000-0000-0000-0000-000000000001"),
            Kind = ProjectAgentLiveUpdateKind.AgentGroupUpserted,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Group = new ProjectAgentGroupLiveDto
            {
                GroupId = earlyGroupId,
                RuntimeKey = "early-group",
                DisplayName = "Early Group",
                CreatedAtUtc = CreatedAt(1),
            },
        });
        state.ApplyLiveUpdate(CreateAgentUpdate(lateGroupId, lateAgentId, "Late Agent", 2));
        state.ApplyLiveUpdate(CreateAgentUpdate(lateGroupId, earlyAgentId, "Early Agent", 1));

        CollectionAssert.AreEqual(
            new[] { earlyGroupId, lateGroupId },
            state.Snapshot.Groups.Select(group => group.GroupId).ToArray());
        CollectionAssert.AreEqual(
            new[] { earlyAgentId, lateAgentId },
            state.Snapshot.Groups.Single(group => group.GroupId == lateGroupId).Agents.Select(agent => agent.AgentId).ToArray());

        state.SelectAgent(earlyAgentId);
        state.ApplyLiveUpdate(CreateTimelineUpdate(earlyAgentId, 2, "second"));
        state.ApplyLiveUpdate(CreateTimelineUpdate(earlyAgentId, 1, "first"));

        CollectionAssert.AreEqual(new long[] { 1, 2 }, state.History.TimelineEntries.Select(entry => entry.Sequence).ToArray());
    }

    [TestMethod]
    public void SelectedTimelineUpdatesChangeHistoryAndNonSelectedTimelineUpdateIsNoOp()
    {
        AgentStatusPageState state = AgentStatusPageState.CreateEmpty();
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000030");
        Guid selectedAgentId = Guid.Parse("72000000-0000-0000-0000-000000000030");
        Guid otherAgentId = Guid.Parse("72000000-0000-0000-0000-000000000031");
        ProjectAgentTimelineEntryDto existingEntry = CreateTimelineEntry(selectedAgentId, 1, "existing");
        state.SetSnapshot(CreateSnapshot(
            [
                CreateGroup(
                    groupId,
                    "Group A",
                    1,
                    [
                        CreateAgent(selectedAgentId, groupId, "Selected", 1, [existingEntry]),
                        CreateAgent(otherAgentId, groupId, "Other", 2, []),
                    ]),
            ]));

        Assert.IsFalse(state.ApplyLiveUpdate(CreateTimelineUpdate(otherAgentId, 1, "ignored")));

        bool upsertChanged = state.ApplyLiveUpdate(CreateTimelineUpdate(selectedAgentId, 2, "new selected"));

        Assert.IsTrue(upsertChanged);
        Assert.HasCount(2, state.History.TimelineEntries);
        Assert.AreEqual(2, state.History.GetLatestSequence());

        bool removeChanged = state.ApplyLiveUpdate(new ProjectAgentLiveUpdateDto
        {
            ProjectId = Guid.Parse("70000000-0000-0000-0000-000000000001"),
            Kind = ProjectAgentLiveUpdateKind.TimelineEntriesRemoved,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            RemovedTimelineEntries = new ProjectAgentTimelineEntriesRemovedDto
            {
                AgentId = selectedAgentId,
                TimelineEntryIds = [existingEntry.TimelineEntryId],
            },
        });

        Assert.IsTrue(removeChanged);
        Assert.HasCount(1, state.History.TimelineEntries);
        Assert.AreEqual("new selected", state.History.TimelineEntries[0].Message);
    }

    [TestMethod]
    public void SetAgentHistoryReplacesSelectedHistoryAndLatestSequenceDefaultsToZero()
    {
        AgentStatusPageState state = AgentStatusPageState.CreateEmpty();
        Guid groupId = Guid.Parse("71000000-0000-0000-0000-000000000040");
        Guid agentId = Guid.Parse("72000000-0000-0000-0000-000000000040");
        state.SetSnapshot(CreateSnapshot(
            [
                CreateGroup(groupId, "Group A", 1, [CreateAgent(agentId, groupId, "Agent", 1, [])]),
            ]));

        Assert.AreEqual(0, state.History.GetLatestSequence());

        state.SetAgentHistory(
            agentId,
            [
                CreateTimelineEntry(agentId, 5, "fifth"),
                CreateTimelineEntry(agentId, 3, "third"),
            ]);

        Assert.AreEqual(agentId, state.History.AgentId);
        Assert.AreEqual(5, state.History.GetLatestSequence());
        CollectionAssert.AreEqual(
            new long[] { 5, 3 },
            state.History.TimelineEntries.Select(entry => entry.Sequence).ToArray());
        CollectionAssert.AreEqual(
            new long[] { 3, 5 },
            state.Snapshot.FindAgent(agentId)!.TimelineEntries.Select(entry => entry.Sequence).ToArray());
    }

    private static ProjectAgentLiveUpdateDto CreateAgentUpdate(Guid groupId, Guid agentId, string displayName, int createdMinute) => new()
    {
        ProjectId = Guid.Parse("70000000-0000-0000-0000-000000000001"),
        Kind = ProjectAgentLiveUpdateKind.AgentUpserted,
        OccurredAtUtc = DateTimeOffset.UtcNow,
        Agent = new ProjectAgentLiveDto
        {
            AgentId = agentId,
            GroupId = groupId,
            RuntimeKey = displayName.ToLowerInvariant().Replace(' ', '-'),
            DisplayName = displayName,
            Status = ProjectAgentRunStatus.Waiting,
            CreatedAtUtc = CreatedAt(createdMinute),
            SystemPrompt = "",
        },
    };

    private static ProjectAgentLiveUpdateDto CreateTimelineUpdate(Guid agentId, long sequence, string message) => new()
    {
        ProjectId = Guid.Parse("70000000-0000-0000-0000-000000000001"),
        Kind = ProjectAgentLiveUpdateKind.TimelineEntryUpserted,
        OccurredAtUtc = DateTimeOffset.UtcNow,
        TimelineEntry = CreateTimelineEntry(agentId, sequence, message),
    };

    private static ProjectAgentStatusSnapshotDto CreateSnapshot(IReadOnlyList<ProjectAgentGroupSnapshotDto> groups) => new()
    {
        ProjectId = Guid.Parse("70000000-0000-0000-0000-000000000001"),
        ProjectStatus = ProjectStatus.Reviewing,
        SnapshotGeneratedAtUtc = CreatedAt(0),
        AgentGroups = groups,
    };

    private static ProjectAgentGroupSnapshotDto CreateGroup(
        Guid groupId,
        string displayName,
        int createdMinute,
        IReadOnlyList<ProjectAgentSnapshotDto> agents) => new()
        {
            GroupId = groupId,
            RuntimeKey = displayName.ToLowerInvariant().Replace(' ', '-'),
            DisplayName = displayName,
            CreatedAtUtc = CreatedAt(createdMinute),
            Agents = agents,
        };

    private static ProjectAgentSnapshotDto CreateAgent(
        Guid agentId,
        Guid groupId,
        string displayName,
        int createdMinute,
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries) => new()
        {
            AgentId = agentId,
            GroupId = groupId,
            RuntimeKey = displayName.ToLowerInvariant().Replace(' ', '-'),
            DisplayName = displayName,
            SystemPrompt = "",
            Status = ProjectAgentRunStatus.Waiting,
            CreatedAtUtc = CreatedAt(createdMinute),
            HasLoadedHistory = timelineEntries.Count > 0,
            TimelineEntries = timelineEntries,
        };

    private static ProjectAgentTimelineEntryDto CreateTimelineEntry(Guid agentId, long sequence, string message) => new()
    {
        TimelineEntryId = Guid.Parse($"73000000-0000-0000-0000-{sequence:000000000000}"),
        AgentId = agentId,
        Sequence = sequence,
        EntryKind = ProjectAgentTimelineEntryKind.Output,
        OccurredAtUtc = CreatedAt((int)sequence),
        Message = message,
    };

    private static DateTimeOffset CreatedAt(int minute) =>
        new(2026, 5, 10, 10, minute, 0, TimeSpan.Zero);
}
