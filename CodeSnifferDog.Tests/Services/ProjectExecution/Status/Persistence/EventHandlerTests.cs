using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Tests.Services.ProjectExecution.Status.Persistence;

[TestClass]
public sealed class EventHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_DispatchesKnownEventsToPersistenceService()
    {
        CapturingPersistenceService persistenceService = new();
        PersistenceEventHandler handler = new(persistenceService);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await handler.HandleAsync(new GroupCreatedEvent { GroupKey = "group", DisplayName = "Group", OccurredAtUtc = now }, CancellationToken.None);
        await handler.HandleAsync(new CreatedEvent { GroupKey = "group", AgentKey = "agent", DisplayName = "Agent", SystemPrompt = "prompt", InitialStatus = "Waiting", OccurredAtUtc = now }, CancellationToken.None);
        await handler.HandleAsync(new StatusChangedEvent { GroupKey = "group", AgentKey = "agent", Status = "Running", OccurredAtUtc = now }, CancellationToken.None);
        await handler.HandleAsync(new UserMessageAppendedEvent { GroupKey = "group", AgentKey = "agent", Message = "input", OccurredAtUtc = now }, CancellationToken.None);
        await handler.HandleAsync(new AssistantMessageAppendedEvent { GroupKey = "group", AgentKey = "agent", Message = "output", OccurredAtUtc = now }, CancellationToken.None);
        await handler.HandleAsync(new ToolCallStartedEvent { GroupKey = "group", AgentKey = "agent", ToolCallId = "call", ToolName = "tool", Arguments = "{}", OccurredAtUtc = now }, CancellationToken.None);
        await handler.HandleAsync(new ToolCallCompletedEvent { GroupKey = "group", AgentKey = "agent", ToolCallId = "call", Result = "result", OccurredAtUtc = now }, CancellationToken.None);
        await handler.HandleAsync(new CompactionEvent { GroupKey = "group", AgentKey = "agent", OccurredAtUtc = now }, CancellationToken.None);
        await handler.HandleAsync(new TranscriptClearedEvent { GroupKey = "group", AgentKey = "agent", ClearAfterUtc = now.AddSeconds(-1), OccurredAtUtc = now }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "group",
                "agent",
                "status",
                "timeline:Input:input",
                "timeline:Output:output",
                "tool-started",
                "tool-completed",
                "timeline:Compaction:",
                "transcript-cleared",
            },
            persistenceService.Calls);
    }

    [TestMethod]
    public async Task HandleAsync_UnknownEventIsNoOp()
    {
        CapturingPersistenceService persistenceService = new();
        PersistenceEventHandler handler = new(persistenceService);

        await handler.HandleAsync(new UnknownStatusEvent { OccurredAtUtc = DateTimeOffset.UtcNow }, CancellationToken.None);

        Assert.IsEmpty(persistenceService.Calls);
    }

    private sealed record UnknownStatusEvent : StatusEvent;

    private sealed class CapturingPersistenceService : IPersistenceService
    {
        public List<string> Calls { get; } = [];

        public Task UpsertGroupAsync(GroupCreatedEvent agentEvent, CancellationToken cancellationToken)
        {
            Calls.Add("group");
            return Task.CompletedTask;
        }

        public Task UpsertAgentAsync(CreatedEvent agentEvent, CancellationToken cancellationToken)
        {
            Calls.Add("agent");
            return Task.CompletedTask;
        }

        public Task UpdateAgentStatusAsync(StatusChangedEvent agentEvent, CancellationToken cancellationToken)
        {
            Calls.Add("status");
            return Task.CompletedTask;
        }

        public Task AppendToolCallStartedEntryAsync(ToolCallStartedEvent agentEvent, CancellationToken cancellationToken)
        {
            Calls.Add("tool-started");
            return Task.CompletedTask;
        }

        public Task CompleteToolCallEntryAsync(ToolCallCompletedEvent agentEvent, CancellationToken cancellationToken)
        {
            Calls.Add("tool-completed");
            return Task.CompletedTask;
        }

        public Task RemoveTranscriptEntriesAsync(TranscriptClearedEvent agentEvent, CancellationToken cancellationToken)
        {
            Calls.Add("transcript-cleared");
            return Task.CompletedTask;
        }

        public Task AppendTimelineEntryAsync(
            string groupKey,
            string agentKey,
            ProjectAgentTimelineEntryType entryType,
            string? message,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken)
        {
            Calls.Add($"timeline:{entryType}:{message}");
            return Task.CompletedTask;
        }
    }
}
