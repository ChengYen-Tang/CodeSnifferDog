using CodeSnifferDog.Models.ReviewAgentTeam;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text;

namespace CodeSnifferDog.Modules.ReviewAgentTeam.Transcript;

/// <summary>
/// Adds transcript-event publication behavior to review-agent builders.
/// </summary>
internal static class AgentBuilderExtensions
{
    /// <summary>
    /// Response additional-properties key used to mark that transcript events were already published for a response.
    /// </summary>
    internal const string ResponseEventsPublishedPropertyName = "CodeSnifferDog.AgentTranscriptEventsPublished";

    /// <summary>
    /// Wraps an agent builder so assistant text and tool activity are published into the supplied event scope.
    /// </summary>
    /// <param name="builder">Agent builder to configure.</param>
    /// <param name="eventScope">Event scope that should receive transcript events.</param>
    /// <returns>The configured agent builder.</returns>
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

    /// <summary>
    /// Adds transcript-event publication only when an event scope is available.
    /// </summary>
    /// <param name="builder">Agent builder to configure.</param>
    /// <param name="eventScope">Optional event scope that should receive transcript events.</param>
    /// <returns>The original builder when <paramref name="eventScope" /> is <see langword="null" />; otherwise the configured builder.</returns>
    public static AIAgentBuilder UseAgentTranscriptEventsIfAvailable(
        this AIAgentBuilder builder,
        IAgentEventScope? eventScope)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return eventScope is null
            ? builder
            : builder.UseAgentTranscriptEvents(eventScope);
    }

    /// <summary>
    /// Determines whether transcript events were already published while producing the supplied response.
    /// </summary>
    /// <param name="response">Agent response to inspect.</param>
    /// <returns><see langword="true" /> when transcript events were already published for <paramref name="response" />.</returns>
    public static bool HasPublishedTranscriptEvents(AgentResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.AdditionalProperties is not null &&
            response.AdditionalProperties.TryGetValue(ResponseEventsPublishedPropertyName, out object? value) &&
            value is true;
    }

    /// <summary>
    /// Runs one non-streaming invocation by consuming the streaming pipeline and marking the response as already published.
    /// </summary>
    /// <param name="messages">Messages to send to the inner agent.</param>
    /// <param name="session">Agent session associated with the invocation.</param>
    /// <param name="runOptions">Optional run options passed to the inner agent.</param>
    /// <param name="innerAgent">Inner agent that produces the response.</param>
    /// <param name="eventScope">Event scope that should receive transcript events.</param>
    /// <param name="cancellationToken">Cancels the invocation and event publication.</param>
    /// <returns>The completed response annotated as already published.</returns>
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

    /// <summary>
    /// Runs one streaming invocation and publishes transcript events while updates are emitted.
    /// </summary>
    /// <param name="messages">Messages to send to the inner agent.</param>
    /// <param name="session">Agent session associated with the invocation.</param>
    /// <param name="runOptions">Optional run options passed to the inner agent.</param>
    /// <param name="innerAgent">Inner agent that produces streaming updates.</param>
    /// <param name="eventScope">Event scope that should receive transcript events.</param>
    /// <param name="cancellationToken">Cancels the invocation and event publication.</param>
    /// <returns>The streaming response updates.</returns>
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

    /// <summary>
    /// Aggregates streaming assistant text and tool payload updates into transcript events.
    /// </summary>
    /// <param name="eventScope">Event scope that should receive the aggregated transcript events.</param>
    private sealed class AgentTranscriptUpdatePublisher(IAgentEventScope eventScope)
    {
        private readonly Dictionary<string, StringBuilder> _assistantTextBuffers = new(StringComparer.Ordinal);
        private readonly HashSet<string> _startedToolCallIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _completedToolCallIds = new(StringComparer.Ordinal);

        /// <summary>
        /// Publishes transcript events implied by one streaming update.
        /// </summary>
        /// <param name="update">Streaming response update to process.</param>
        /// <param name="cancellationToken">Cancels event publication.</param>
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

        /// <summary>
        /// Flushes all buffered assistant text messages into transcript events.
        /// </summary>
        /// <param name="cancellationToken">Cancels event publication.</param>
        public async ValueTask FlushAsync(CancellationToken cancellationToken)
        {
            foreach (string messageKey in _assistantTextBuffers.Keys.ToArray())
                await FlushAssistantTextAsync(messageKey, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the text buffer associated with the logical assistant message represented by the update.
        /// </summary>
        /// <param name="update">Streaming update whose assistant text should be buffered.</param>
        /// <returns>The mutable buffer for the logical assistant message.</returns>
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

        /// <summary>
        /// Publishes one buffered assistant message and clears its buffer.
        /// </summary>
        /// <param name="messageKey">Logical assistant message key whose buffer should be flushed.</param>
        /// <param name="cancellationToken">Cancels event publication.</param>
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

        /// <summary>
        /// Publishes tool call start and completion events while suppressing duplicate call identifiers.
        /// </summary>
        /// <param name="contents">Streaming content payloads to inspect.</param>
        /// <param name="cancellationToken">Cancels event publication.</param>
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

        /// <summary>
        /// Determines whether the supplied content payloads include tool calls or tool results.
        /// </summary>
        /// <param name="contents">Content payloads to inspect.</param>
        /// <returns><see langword="true" /> when the payload contains tool call or tool result content.</returns>
        private static bool ContainsToolContent(IEnumerable<AIContent> contents) =>
            contents.Any(static content => content is FunctionCallContent or FunctionResultContent);

        /// <summary>
        /// Chooses the best available stable key for grouping streaming assistant text fragments.
        /// </summary>
        /// <param name="update">Streaming update whose logical message key should be derived.</param>
        /// <returns>A stable grouping key for assistant text buffering.</returns>
        private static string GetMessageKey(AgentResponseUpdate update) =>
            update.MessageId ??
            update.ResponseId ??
            update.AgentId ??
            "__default_assistant_message";
    }
}
