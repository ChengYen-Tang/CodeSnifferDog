using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Workflows.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSnifferDog.Agents.Common;

/// <summary>
/// Invokes the existing reducer immediately before a provider call.
/// </summary>
/// <remarks>
/// This adapter belongs below <see cref="FunctionInvokingChatClient"/> so each function-loop iteration
/// reaches the established compaction module without changing that module's behavior.
/// </remarks>
internal sealed class CompactingChatClient(
    IChatClient innerClient,
    AgentCompactionOptions options,
    ILoggerFactory? loggerFactory = null) : IChatClient
{
    private readonly IChatClient _innerClient = innerClient;
    private readonly AgentCompactionOptions _options = options;
    private readonly ContextPreparationService _preparation = new(options);
    private readonly object _attemptStateKey = new();
    private readonly ILogger _logger = loggerFactory?.CreateLogger("CodeSnifferDog.Agents.TokenUsage") ?? NullLogger.Instance;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        int? callNumber = AgentRunAttemptContext.GetNextModelCallNumber();
        IEnumerable<ChatMessage> compactedMessages = await PrepareAsync(messages, callNumber, cancellationToken).ConfigureAwait(false);
        ChatResponse response = await _innerClient.GetResponseAsync(compactedMessages, options, cancellationToken).ConfigureAwait(false);
        LogUsage(response.Usage, response.ModelId ?? options?.ModelId, callNumber);
        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int? callNumber = AgentRunAttemptContext.GetNextModelCallNumber();
        IEnumerable<ChatMessage> compactedMessages = await PrepareAsync(messages, callNumber, cancellationToken).ConfigureAwait(false);

        await foreach (ChatResponseUpdate update in _innerClient
            .GetStreamingResponseAsync(compactedMessages, options, cancellationToken)
            .ConfigureAwait(false))
        {
            foreach (UsageContent usage in update.Contents.OfType<UsageContent>())
                LogUsage(usage.Details, update.ModelId ?? options?.ModelId, callNumber);
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        _innerClient.GetService(serviceType, serviceKey);

    // The provider client is owned by the composition root and may be shared.
    public void Dispose() { }

    private async Task<IReadOnlyList<ChatMessage>> PrepareAsync(
        IEnumerable<ChatMessage> messages,
        int? callNumber,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ChatMessage> originalMessages = messages as IReadOnlyList<ChatMessage> ?? [.. messages];
        AttemptPreparationState? state = GetAttemptState();
        ContextPreparationState automaticState = state?.AutomaticState ?? new ContextPreparationState();
        IReadOnlyList<ChatMessage> messagesToPrepare = state?.Checkpoint?.BuildInput(originalMessages) ?? originalMessages;
        IReadOnlyList<ChatMessage> compactedMessages = await _preparation
            .PrepareAsync(messagesToPrepare, session: null, automaticState, cancellationToken)
            .ConfigureAwait(false);
        state?.UpdateCheckpoint(originalMessages, compactedMessages);
        _logger.LogDebug(
            "Prepared per-call model context. GroupKey: {GroupKey}; AgentKey: {AgentKey}; AttemptId: {AttemptId}; CallNumber: {CallNumber}; EstimatedInputTokensBefore: {EstimatedInputTokensBefore}; EstimatedInputTokens: {EstimatedInputTokens}; AutomaticThreshold: {AutomaticThreshold}; WasCompacted: {WasCompacted}.",
            AgentRunAttemptContext.CurrentAgentGroupKey,
            AgentRunAttemptContext.CurrentAgentKey,
            AgentRunAttemptContext.CurrentAttemptId,
            callNumber,
            TokenEstimator.Estimate(originalMessages),
            TokenEstimator.Estimate(compactedMessages),
            _options.Reducer.Options.GetAutoCompactThreshold(),
            compactedMessages.Any(message => message.AdditionalProperties?.ContainsKey(CompactionArtifactMetadata.IsCompactionSummaryKey) == true));
        return compactedMessages;
    }

    private AttemptPreparationState? GetAttemptState() =>
        AgentRunAttemptContext.GetOrCreateAttemptState(_attemptStateKey, static () => new AttemptPreparationState());

    private void LogUsage(UsageDetails? usage, string? model, int? callNumber)
    {
        if (usage is null)
            return;

        _logger.LogDebug(
            "LLM usage received. GroupKey: {GroupKey}; AgentKey: {AgentKey}; AttemptId: {AttemptId}; CallNumber: {CallNumber}; Model: {Model}; InputTokens: {InputTokens}; OutputTokens: {OutputTokens}; TotalTokens: {TotalTokens}.",
            AgentRunAttemptContext.CurrentAgentGroupKey,
            AgentRunAttemptContext.CurrentAgentKey,
            AgentRunAttemptContext.CurrentAttemptId,
            callNumber,
            model,
            usage.InputTokenCount,
            usage.OutputTokenCount,
            usage.TotalTokenCount);
    }

    private sealed class AttemptPreparationState
    {
        public ContextPreparationState AutomaticState { get; } = new();
        public CompactionCheckpoint? Checkpoint { get; private set; }

        public void UpdateCheckpoint(IReadOnlyList<ChatMessage> rawMessages, IReadOnlyList<ChatMessage> preparedMessages)
        {
            if (preparedMessages.Any(message => message.AdditionalProperties?.ContainsKey(CompactionArtifactMetadata.IsCompactionSummaryKey) == true))
                Checkpoint = new CompactionCheckpoint(rawMessages, preparedMessages);
        }
    }

    private sealed class CompactionCheckpoint(IReadOnlyList<ChatMessage> rawMessages, IReadOnlyList<ChatMessage> preparedMessages)
    {
        private readonly IReadOnlyList<ChatMessage> _rawMessages = [.. rawMessages];
        private readonly IReadOnlyList<ChatMessage> _preparedMessages = [.. preparedMessages];

        public IReadOnlyList<ChatMessage>? BuildInput(IReadOnlyList<ChatMessage> currentRawMessages)
        {
            if (currentRawMessages.Count < _rawMessages.Count ||
                !_rawMessages.Select((message, index) => ReferenceEquals(message, currentRawMessages[index])).All(static matches => matches))
                return null;

            return [.. _preparedMessages, .. currentRawMessages.Skip(_rawMessages.Count)];
        }
    }
}
