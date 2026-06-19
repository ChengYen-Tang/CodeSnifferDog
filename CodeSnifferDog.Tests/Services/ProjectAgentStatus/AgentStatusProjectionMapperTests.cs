using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Tests.Services.ProjectAgentStatus;

[TestClass]
public sealed class AgentStatusProjectionMapperTests
{
    [TestMethod]
    public void MapProjectStatus_MapsPersistedProjectStatus()
    {
        AgentStatusProjectionMapper mapper = new();

        Assert.AreEqual(ProjectStatus.Queued, mapper.MapProjectStatus(ProjectProcessingStatus.Queued));
        Assert.AreEqual(ProjectStatus.Reviewing, mapper.MapProjectStatus(ProjectProcessingStatus.Reviewing));
        Assert.AreEqual(ProjectStatus.Completed, mapper.MapProjectStatus(ProjectProcessingStatus.Completed));
        Assert.AreEqual(ProjectStatus.Failed, mapper.MapProjectStatus(ProjectProcessingStatus.Failed));
        Assert.AreEqual(ProjectStatus.Canceled, mapper.MapProjectStatus(ProjectProcessingStatus.Canceled));
    }

    [TestMethod]
    public void MapAgentStatus_MapsPersistedAgentStatus()
    {
        AgentStatusProjectionMapper mapper = new();

        Assert.AreEqual(ProjectAgentRunStatus.Waiting, mapper.MapAgentStatus(PersistedAgentStatus.Waiting));
        Assert.AreEqual(ProjectAgentRunStatus.Running, mapper.MapAgentStatus(PersistedAgentStatus.Running));
        Assert.AreEqual(ProjectAgentRunStatus.Completed, mapper.MapAgentStatus(PersistedAgentStatus.Completed));
        Assert.AreEqual(ProjectAgentRunStatus.Degraded, mapper.MapAgentStatus(PersistedAgentStatus.Degraded));
    }

    [TestMethod]
    public void MapTimelineEntryKind_MapsPersistedTimelineEntryType()
    {
        AgentStatusProjectionMapper mapper = new();

        Assert.AreEqual(ProjectAgentTimelineEntryKind.Input, mapper.MapTimelineEntryKind(ProjectAgentTimelineEntryType.Input));
        Assert.AreEqual(ProjectAgentTimelineEntryKind.Output, mapper.MapTimelineEntryKind(ProjectAgentTimelineEntryType.Output));
        Assert.AreEqual(ProjectAgentTimelineEntryKind.Tool, mapper.MapTimelineEntryKind(ProjectAgentTimelineEntryType.Tool));
        Assert.AreEqual(ProjectAgentTimelineEntryKind.Compaction, mapper.MapTimelineEntryKind(ProjectAgentTimelineEntryType.Compaction));
    }

    [TestMethod]
    public void MapDtos_MapsPersistedRecordsToSharedDtos()
    {
        AgentStatusProjectionMapper mapper = new();
        Guid projectId = Guid.NewGuid();
        Guid groupId = Guid.NewGuid();
        Guid agentId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProjectAgentGroupRecord group = new()
        {
            Id = groupId,
            ProjectId = projectId,
            RuntimeKey = "group",
            DisplayName = "Group",
            CreatedAtUtc = now,
        };
        ProjectAgentRecord agent = new()
        {
            Id = agentId,
            ProjectAgentGroupId = groupId,
            RuntimeKey = "agent",
            DisplayName = "Agent",
            SystemPrompt = "prompt",
            Status = PersistedAgentStatus.Running,
            CreatedAtUtc = now,
        };
        ProjectAgentTimelineEntryRecord entry = new()
        {
            Id = Guid.NewGuid(),
            ProjectAgentId = agentId,
            Sequence = 7,
            EntryType = ProjectAgentTimelineEntryType.Tool,
            OccurredAtUtc = now,
            ToolCallId = "call",
            ToolName = "tool",
            ToolArguments = "{}",
            ToolResult = "result",
        };

        ProjectAgentGroupLiveDto groupDto = mapper.MapGroup(group);
        ProjectAgentLiveDto agentDto = mapper.MapAgent(agent);
        ProjectAgentTimelineEntryDto entryDto = mapper.MapTimelineEntry(entry);

        Assert.AreEqual(groupId, groupDto.GroupId);
        Assert.AreEqual("group", groupDto.RuntimeKey);
        Assert.AreEqual("Group", groupDto.DisplayName);
        Assert.AreEqual(agentId, agentDto.AgentId);
        Assert.AreEqual(groupId, agentDto.GroupId);
        Assert.AreEqual(ProjectAgentRunStatus.Running, agentDto.Status);
        Assert.AreEqual(entry.Id, entryDto.TimelineEntryId);
        Assert.AreEqual(agentId, entryDto.AgentId);
        Assert.AreEqual(ProjectAgentTimelineEntryKind.Tool, entryDto.EntryKind);
        Assert.AreEqual("result", entryDto.ToolResult);
    }

    [TestMethod]
    public void UnsupportedPersistedValues_ThrowOriginalExceptions()
    {
        AgentStatusProjectionMapper mapper = new();

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
}
