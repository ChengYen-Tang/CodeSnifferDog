using CodeSnifferDog.Models.ReviewAgentTeam;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text;

namespace CodeSnifferDog.Modules.ReviewAgentTeam.Transcript;

internal static class AgentTranscriptEventAgentBuilderExtensions
{
    internal const string ResponseEventsPublishedPropertyName = "CodeSnifferDog.AgentTranscriptEventsPublished";

    public static AIAgentBuilder UseAgentTranscriptEvents(
        this AIAgentBuilder builder,
        IAgentEventScope eventScope)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(eventScope);

        return builder.Use(
            (messages, session, runOptions, innerAgent, cancellationToken) =>
                RunAndPublishAsync(messages, session, runOptions, innerAgent, eventScope, cancellationToken),
            (messages, session, runOptions, innerAgent, cancellationToken) =>
                RunStreamingAndPublishAsync(messages, session, runOptions, innerAgent, eventScope, cancellationToken));
    }

    public static AIAgentBuilder UseAgentTranscriptEventsIfAvailable(
        this AIAgentBuilder builder,
        IAgentEventScope? eventScope)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return eventScope is null
            ? builder
            : builder.UseAgentTranscriptEvents(eventScope);
    }

    public static bool HasPublishedTranscriptEvents(AgentResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.AdditionalProperties is not null &&
            response.AdditionalProperties.TryGetValue(ResponseEventsPublishedPropertyName, out object? value) &&
            value is true;
    }

    private static async Task<AgentResponse> RunAndPublishAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? runOptions,
        AIAgent innerAgent,
        IAgentEventScope eventScope,
        CancellationToken cancellationToken)
    {
        AgentResponse response = await RunStreamingAndPublishAsync(
            messages,
            session,
            runOptions,
            innerAgent,
            eventScope,
            cancellationToken).ToAgentResponseAsync(cancellationToken).ConfigureAwait(false);

        response.AdditionalProperties ??= [];
        response.AdditionalProperties[ResponseEventsPublishedPropertyName] = true;
        return response;
    }

    private static async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAndPublishAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? runOptions,
        AIAgent innerAgent,
        IAgentEventScope eventScope,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        AgentTranscriptUpdatePublisher publisher = new(eventScope);

        await foreach (AgentResponseUpdate update in innerAgent
            .RunStreamingAsync(messages, session, runOptions, cancellationToken)
            .ConfigureAwait(false))
        {
            await publisher.PublishAsync(update, cancellationToken).ConfigureAwait(false);
            yield return update;
        }

        await publisher.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class AgentTranscriptUpdatePublisher(IAgentEventScope eventScope)
    {
        private readonly Dictionary<string, StringBuilder> _assistantTextBuffers = new(StringComparer.Ordinal);
        private readonly HashSet<string> _startedToolCallIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _completedToolCallIds = new(StringComparer.Ordinal);

        public async ValueTask PublishAsync(
            AgentResponseUpdate update,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(update);

            string text = update.Role == ChatRole.Assistant ? update.Text : string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
                GetAssistantBuffer(update).Append(text);

            if (ContainsToolContent(update.Contents))
            {
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                await PublishToolContentAsync(update.Contents, cancellationToken).ConfigureAwait(false);
            }

            if (update.FinishReason is not null)
                await FlushAssistantTextAsync(GetMessageKey(update), cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            foreach (string messageKey in _assistantTextBuffers.Keys.ToArray())
                await FlushAssistantTextAsync(messageKey, cancellationToken).ConfigureAwait(false);
        }

        private StringBuilder GetAssistantBuffer(AgentResponseUpdate update)
        {
            string messageKey = GetMessageKey(update);
            if (!_assistantTextBuffers.TryGetValue(messageKey, out StringBuilder? buffer))
            {
                buffer = new StringBuilder();
                _assistantTextBuffers.Add(messageKey, buffer);
            }

            return buffer;
        }

        private async ValueTask FlushAssistantTextAsync(
            string messageKey,
            CancellationToken cancellationToken)
        {
            if (!_assistantTextBuffers.Remove(messageKey, out StringBuilder? buffer))
                return;

            string message = buffer.ToString();
            if (!string.IsNullOrWhiteSpace(message))
                await eventScope.PublishAssistantMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask PublishToolContentAsync(
            IEnumerable<AIContent> contents,
            CancellationToken cancellationToken)
        {
            foreach (AIContent content in contents)
            {
                if (content is FunctionCallContent functionCall &&
                    _startedToolCallIds.Add(functionCall.CallId))
                {
                    await AgentToolEventPublisher.PublishStartedAsync(
                        functionCall,
                        eventScope,
                        cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (content is FunctionResultContent functionResult &&
                    _completedToolCallIds.Add(functionResult.CallId))
                {
                    await AgentToolEventPublisher.PublishCompletedAsync(
                        functionResult,
                        eventScope,
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static bool ContainsToolContent(IEnumerable<AIContent> contents) =>
            contents.Any(static content => content is FunctionCallContent or FunctionResultContent);

        private static string GetMessageKey(AgentResponseUpdate update) =>
            update.MessageId ??
            update.ResponseId ??
            update.AgentId ??
            "__default_assistant_message";
    }
}
