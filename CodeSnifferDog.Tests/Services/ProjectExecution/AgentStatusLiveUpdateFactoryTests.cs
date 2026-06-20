using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;
using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class AgentStatusLiveUpdateFactoryTests
{
    [TestMethod]
    public void CreateUpdates_MapsPersistedRecordsToLiveUpdateDtos()
    {
        AgentStatusLiveUpdateFactory factory = new(new AgentStatusProjectionMapper());
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
            Status = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus.Running,
            CreatedAtUtc = now,
        };
        ProjectAgentTimelineEntryRecord entry = new()
        {
            Id = Guid.NewGuid(),
            ProjectAgentId = agentId,
            Sequence = 3,
            EntryType = ProjectAgentTimelineEntryType.Tool,
            ToolCallId = "call",
            ToolName = "tool",
            ToolArguments = "{}",
            ToolResult = "result",
            OccurredAtUtc = now,
        };

        ProjectAgentLiveUpdateDto groupUpdate = factory.CreateGroupUpdate(projectId, group);
        ProjectAgentLiveUpdateDto agentUpdate = factory.CreateAgentUpsertUpdate(projectId, agent);
        ProjectAgentLiveUpdateDto statusUpdate = factory.CreateAgentStatusChangedUpdate(projectId, agentId, agent.Status, now);
        ProjectAgentLiveUpdateDto timelineUpdate = factory.CreateTimelineEntryUpsertUpdate(projectId, entry);
        ProjectAgentLiveUpdateDto removedUpdate = factory.CreateTimelineEntriesRemovedUpdate(projectId, agentId, [entry.Id], now);

        Assert.AreEqual(ProjectAgentLiveUpdateKind.AgentGroupUpserted, groupUpdate.Kind);
        Assert.AreEqual(groupId, groupUpdate.Group!.GroupId);
        Assert.AreEqual(ProjectAgentRunStatus.Running, agentUpdate.Agent!.Status);
        Assert.AreEqual(ProjectAgentRunStatus.Running, statusUpdate.AgentStatus!.Status);
        Assert.AreEqual(ProjectAgentTimelineEntryKind.Tool, timelineUpdate.TimelineEntry!.EntryKind);
        Assert.AreEqual("result", timelineUpdate.TimelineEntry.ToolResult);
        Assert.AreEqual(agentId, removedUpdate.RemovedTimelineEntries!.AgentId);
        CollectionAssert.AreEqual(new[] { entry.Id }, removedUpdate.RemovedTimelineEntries.TimelineEntryIds.ToArray());
    }

    [TestMethod]
    public void MapAgentStatus_UnsupportedValueThrowsOriginalException()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => new AgentStatusProjectionMapper().MapAgentStatus((CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus)999));

        Assert.AreEqual("Unsupported persisted agent status '999'.", exception.Message);
    }

    [TestMethod]
    public void MapTimelineEntryKind_UnsupportedValueThrowsOriginalException()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => new AgentStatusProjectionMapper().MapTimelineEntryKind((ProjectAgentTimelineEntryType)999));

        Assert.AreEqual("Unsupported persisted timeline entry type '999'.", exception.Message);
    }
}
