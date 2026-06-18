using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal sealed class AgentStatusEventHandler(
    IAgentStatusPersistenceService persistenceService) : IAgentStatusEventHandler
{
    private readonly IAgentStatusPersistenceService _persistenceService = persistenceService;

    public async Task HandleAsync(AgentStatusEvent agentEvent, CancellationToken cancellationToken)
    {
        switch (agentEvent)
        {
            case AgentGroupCreatedEvent groupCreatedEvent:
                await _persistenceService.UpsertGroupAsync(groupCreatedEvent, cancellationToken).ConfigureAwait(false);
                return;

            case AgentCreatedEvent agentCreatedEvent:
                await _persistenceService.UpsertAgentAsync(agentCreatedEvent, cancellationToken).ConfigureAwait(false);
                return;

            case AgentStatusChangedEvent statusChangedEvent:
                await _persistenceService.UpdateAgentStatusAsync(statusChangedEvent, cancellationToken).ConfigureAwait(false);
                return;

            case UserMessageAppendedEvent userMessageEvent:
                await _persistenceService.AppendTimelineEntryAsync(
                    userMessageEvent.GroupKey,
                    userMessageEvent.AgentKey,
                    ProjectAgentTimelineEntryType.Input,
                    userMessageEvent.Message,
                    userMessageEvent.OccurredAtUtc,
                    cancellationToken).ConfigureAwait(false);
                return;

            case AssistantMessageAppendedEvent assistantMessageEvent:
                await _persistenceService.AppendTimelineEntryAsync(
                    assistantMessageEvent.GroupKey,
                    assistantMessageEvent.AgentKey,
                    ProjectAgentTimelineEntryType.Output,
                    assistantMessageEvent.Message,
                    assistantMessageEvent.OccurredAtUtc,
                    cancellationToken).ConfigureAwait(false);
                return;

            case ToolCallStartedEvent toolCallStartedEvent:
                await _persistenceService.AppendToolCallStartedEntryAsync(toolCallStartedEvent, cancellationToken).ConfigureAwait(false);
                return;

            case ToolCallCompletedEvent toolCallCompletedEvent:
                await _persistenceService.CompleteToolCallEntryAsync(toolCallCompletedEvent, cancellationToken).ConfigureAwait(false);
                return;

            case AgentCompactionEvent compactionEvent:
                await _persistenceService.AppendTimelineEntryAsync(
                    compactionEvent.GroupKey,
                    compactionEvent.AgentKey,
                    ProjectAgentTimelineEntryType.Compaction,
                    message: null,
                    compactionEvent.OccurredAtUtc,
                    cancellationToken).ConfigureAwait(false);
                return;

            case AgentTranscriptClearedEvent transcriptClearedEvent:
                await _persistenceService.RemoveTranscriptEntriesAsync(transcriptClearedEvent, cancellationToken).ConfigureAwait(false);
                return;
        }
    }
}
