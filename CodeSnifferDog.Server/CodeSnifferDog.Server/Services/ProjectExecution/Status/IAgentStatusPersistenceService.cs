using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal interface IAgentStatusPersistenceService
{
    Task UpsertGroupAsync(AgentGroupCreatedEvent agentEvent, CancellationToken cancellationToken);

    Task UpsertAgentAsync(AgentCreatedEvent agentEvent, CancellationToken cancellationToken);

    Task UpdateAgentStatusAsync(AgentStatusChangedEvent agentEvent, CancellationToken cancellationToken);

    Task AppendToolCallStartedEntryAsync(ToolCallStartedEvent agentEvent, CancellationToken cancellationToken);

    Task CompleteToolCallEntryAsync(ToolCallCompletedEvent agentEvent, CancellationToken cancellationToken);

    Task RemoveTranscriptEntriesAsync(AgentTranscriptClearedEvent agentEvent, CancellationToken cancellationToken);

    Task AppendTimelineEntryAsync(
        string groupKey,
        string agentKey,
        ProjectAgentTimelineEntryType entryType,
        string? message,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);
}
