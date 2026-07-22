using System.Text.Json;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Tests.Services.ProjectExecution.Status.Persistence;

[TestClass]
public sealed class LiveUpdateFactoryTests
{
    [TestMethod]
    public void CreateUpdates_MapsPersistedRecordsToLiveUpdateDtos()
    {
        LiveUpdateFactory factory = new(CreateMapper());
        Guid projectId = Guid.CreateVersion7();
        Guid groupId = Guid.CreateVersion7();
        Guid agentId = Guid.CreateVersion7();
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
            Id = Guid.CreateVersion7(),
            ProjectAgentId = agentId,
            Sequence = 3,
            EntryType = ProjectAgentTimelineEntryType.Tool,
            ToolCallId = "call",
            ToolName = "tool",
            ToolArguments = "{}",
            ToolResult = "result",
            OccurredAtUtc = now,
        };

        LiveUpdateDto groupUpdate = factory.CreateGroupUpdate(projectId, group);
        LiveUpdateDto agentUpdate = factory.CreateAgentUpsertUpdate(projectId, agent);
        LiveUpdateDto statusUpdate = factory.CreateAgentStatusChangedUpdate(projectId, agentId, agent.Status, now);
        LiveUpdateDto timelineUpdate = factory.CreateTimelineEntryUpsertUpdate(projectId, entry);
        LiveUpdateDto removedUpdate = factory.CreateTimelineEntriesRemovedUpdate(projectId, agentId, [entry.Id], now);

        Assert.AreEqual(LiveUpdateKind.AgentGroupUpserted, groupUpdate.Kind);
        Assert.AreEqual(groupId, groupUpdate.Group!.GroupId);
        Assert.AreEqual(RunStatus.Running, agentUpdate.Agent!.Status);
        Assert.AreEqual("prompt", agentUpdate.Agent.SystemPrompt);
        Assert.Contains("\"SystemPrompt\":\"prompt\"", JsonSerializer.Serialize(agentUpdate));
        Assert.AreEqual(RunStatus.Running, statusUpdate.AgentStatus!.Status);
        Assert.AreEqual(TimelineEntryKind.Tool, timelineUpdate.TimelineEntry!.EntryKind);
        Assert.AreEqual("result", timelineUpdate.TimelineEntry.ToolResult);
        Assert.AreEqual(agentId, removedUpdate.RemovedTimelineEntries!.AgentId);
        CollectionAssert.AreEqual(new[] { entry.Id }, removedUpdate.RemovedTimelineEntries.TimelineEntryIds.ToArray());
    }

    [TestMethod]
    public void MapAgentStatus_UnsupportedValueThrowsOriginalException()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => CreateMapper().MapAgentStatus((CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus)999));

        Assert.AreEqual("Unsupported persisted agent status '999'.", exception.Message);
    }

    [TestMethod]
    public void MapTimelineEntryKind_UnsupportedValueThrowsOriginalException()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => CreateMapper().MapTimelineEntryKind((ProjectAgentTimelineEntryType)999));

        Assert.AreEqual("Unsupported persisted timeline entry type '999'.", exception.Message);
    }

    private static ProjectionMapper CreateMapper() => new(new ProjectStatusMapper());
}
