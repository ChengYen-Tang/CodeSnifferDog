using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Tests.Services.ProjectAgentStatus;

[TestClass]
public sealed class ProjectionMapperTests
{
    [TestMethod]
    public void MapProjectStatus_MapsPersistedProjectStatus()
    {
        ProjectionMapper mapper = CreateMapper();

        Assert.AreEqual(ProjectStatus.Queued, mapper.MapProjectStatus(ProjectProcessingStatus.Queued));
        Assert.AreEqual(ProjectStatus.Reviewing, mapper.MapProjectStatus(ProjectProcessingStatus.Reviewing));
        Assert.AreEqual(ProjectStatus.Completed, mapper.MapProjectStatus(ProjectProcessingStatus.Completed));
        Assert.AreEqual(ProjectStatus.Failed, mapper.MapProjectStatus(ProjectProcessingStatus.Failed));
        Assert.AreEqual(ProjectStatus.Canceled, mapper.MapProjectStatus(ProjectProcessingStatus.Canceled));
    }

    [TestMethod]
    public void MapAgentStatus_MapsPersistedAgentStatus()
    {
        ProjectionMapper mapper = CreateMapper();

        Assert.AreEqual(ProjectAgentRunStatus.Waiting, mapper.MapAgentStatus(PersistedAgentStatus.Waiting));
        Assert.AreEqual(ProjectAgentRunStatus.Running, mapper.MapAgentStatus(PersistedAgentStatus.Running));
        Assert.AreEqual(ProjectAgentRunStatus.Completed, mapper.MapAgentStatus(PersistedAgentStatus.Completed));
        Assert.AreEqual(ProjectAgentRunStatus.Degraded, mapper.MapAgentStatus(PersistedAgentStatus.Degraded));
    }

    [TestMethod]
    public void MapTimelineEntryKind_MapsPersistedTimelineEntryType()
    {
        ProjectionMapper mapper = CreateMapper();

        Assert.AreEqual(ProjectAgentTimelineEntryKind.Input, mapper.MapTimelineEntryKind(ProjectAgentTimelineEntryType.Input));
        Assert.AreEqual(ProjectAgentTimelineEntryKind.Output, mapper.MapTimelineEntryKind(ProjectAgentTimelineEntryType.Output));
        Assert.AreEqual(ProjectAgentTimelineEntryKind.Tool, mapper.MapTimelineEntryKind(ProjectAgentTimelineEntryType.Tool));
        Assert.AreEqual(ProjectAgentTimelineEntryKind.Compaction, mapper.MapTimelineEntryKind(ProjectAgentTimelineEntryType.Compaction));
    }

    [TestMethod]
    public void MapDtos_MapsPersistedRecordsToSharedDtos()
    {
        ProjectionMapper mapper = CreateMapper();
        Guid groupId = Guid.NewGuid();
        Guid agentId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid timelineEntryId = Guid.NewGuid();
        GroupProjection group = new(groupId, "group", "Group", now);
        AgentProjection agent = new(
            agentId,
            groupId,
            "agent",
            "Agent",
            "prompt",
            PersistedAgentStatus.Running,
            now);
        TimelineEntryProjection entry = new(
            timelineEntryId,
            agentId,
            7,
            ProjectAgentTimelineEntryType.Tool,
            now,
            null,
            "call",
            "tool",
            "{}",
            "result");

        ProjectAgentGroupLiveDto groupDto = mapper.MapGroup(group);
        ProjectAgentLiveDto agentDto = mapper.MapAgent(agent);
        ProjectAgentTimelineEntryDto entryDto = mapper.MapTimelineEntry(entry);

        Assert.AreEqual(groupId, groupDto.GroupId);
        Assert.AreEqual("group", groupDto.RuntimeKey);
        Assert.AreEqual("Group", groupDto.DisplayName);
        Assert.AreEqual(agentId, agentDto.AgentId);
        Assert.AreEqual(groupId, agentDto.GroupId);
        Assert.AreEqual(ProjectAgentRunStatus.Running, agentDto.Status);
        Assert.AreEqual(timelineEntryId, entryDto.TimelineEntryId);
        Assert.AreEqual(agentId, entryDto.AgentId);
        Assert.AreEqual(ProjectAgentTimelineEntryKind.Tool, entryDto.EntryKind);
        Assert.AreEqual("result", entryDto.ToolResult);
    }

    [TestMethod]
    public void UnsupportedPersistedValues_ThrowOriginalExceptions()
    {
        ProjectionMapper mapper = CreateMapper();

        InvalidOperationException projectStatusException = Assert.ThrowsExactly<InvalidOperationException>(
            () => mapper.MapProjectStatus((ProjectProcessingStatus)999));
        InvalidOperationException agentStatusException = Assert.ThrowsExactly<InvalidOperationException>(
            () => mapper.MapAgentStatus((PersistedAgentStatus)999));
        InvalidOperationException timelineKindException = Assert.ThrowsExactly<InvalidOperationException>(
            () => mapper.MapTimelineEntryKind((ProjectAgentTimelineEntryType)999));

        Assert.AreEqual("Unsupported project status '999'.", projectStatusException.Message);
        Assert.AreEqual("Unsupported persisted agent status '999'.", agentStatusException.Message);
        Assert.AreEqual("Unsupported persisted timeline entry type '999'.", timelineKindException.Message);
    }

    [TestMethod]
    public void UnsupportedSnapshotValues_ThrowSnapshotCompatibleExceptions()
    {
        ProjectionMapper mapper = CreateMapper();

        InvalidOperationException agentStatusException = Assert.ThrowsExactly<InvalidOperationException>(
            () => mapper.MapAgentStatus(
                (PersistedAgentStatus)999,
                ExceptionStyle.Snapshot));
        InvalidOperationException timelineKindException = Assert.ThrowsExactly<InvalidOperationException>(
            () => mapper.MapTimelineEntryKind(
                (ProjectAgentTimelineEntryType)999,
                ExceptionStyle.Snapshot));

        Assert.AreEqual("Unsupported agent status '999'.", agentStatusException.Message);
        Assert.AreEqual("Unsupported timeline entry type '999'.", timelineKindException.Message);
    }

    private static ProjectionMapper CreateMapper() => new(new ProjectStatusMapper());
}
