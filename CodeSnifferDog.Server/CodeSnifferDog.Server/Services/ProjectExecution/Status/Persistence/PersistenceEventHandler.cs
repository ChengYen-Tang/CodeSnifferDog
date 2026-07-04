using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

/// <summary>
/// Maps runtime status events to persistence operations.
/// </summary>
internal sealed class PersistenceEventHandler(
    IPersistenceService persistenceService) : IEventHandler
{
    private readonly IPersistenceService _persistenceService = persistenceService;

    /// <inheritdoc />
    public async Task HandleAsync(StatusEvent agentEvent, CancellationToken cancellationToken)
    {
        switch (agentEvent)
        {
            case GroupCreatedEvent groupCreatedEvent:
                await _persistenceService.UpsertGroupAsync(groupCreatedEvent, cancellationToken).ConfigureAwait(false);
                return;

            case CreatedEvent agentCreatedEvent:
                await _persistenceService.UpsertAgentAsync(agentCreatedEvent, cancellationToken).ConfigureAwait(false);
                return;

            case StatusChangedEvent statusChangedEvent:
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

            case CompactionEvent compactionEvent:
                await _persistenceService.AppendTimelineEntryAsync(
                    compactionEvent.GroupKey,
                    compactionEvent.AgentKey,
                    ProjectAgentTimelineEntryType.Compaction,
                    message: null,
                    compactionEvent.OccurredAtUtc,
                    cancellationToken).ConfigureAwait(false);
                return;

            case TranscriptClearedEvent transcriptClearedEvent:
                await _persistenceService.RemoveTranscriptEntriesAsync(transcriptClearedEvent, cancellationToken).ConfigureAwait(false);
                return;
        }
    }
}
