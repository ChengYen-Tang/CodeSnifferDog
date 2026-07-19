using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Agents.Common.TokenUsage;
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
        AttemptPreparationState? state = GetAttemptState();
        PreparedModelContext preparedContext = await PrepareAsync(messages, state, callNumber, cancellationToken).ConfigureAwait(false);
        ChatResponse response = await _innerClient.GetResponseAsync(preparedContext.Messages, options, cancellationToken).ConfigureAwait(false);
        LogUsage(response.Usage, response.ModelId ?? options?.ModelId, callNumber, preparedContext.Prediction, state?.InputTokenCalibration);
        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int? callNumber = AgentRunAttemptContext.GetNextModelCallNumber();
        AttemptPreparationState? state = GetAttemptState();
        PreparedModelContext preparedContext = await PrepareAsync(messages, state, callNumber, cancellationToken).ConfigureAwait(false);

        await foreach (ChatResponseUpdate update in _innerClient
            .GetStreamingResponseAsync(preparedContext.Messages, options, cancellationToken)
            .ConfigureAwait(false))
        {
            foreach (UsageContent usage in update.Contents.OfType<UsageContent>())
                LogUsage(usage.Details, update.ModelId ?? options?.ModelId, callNumber, preparedContext.Prediction, state?.InputTokenCalibration);
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        _innerClient.GetService(serviceType, serviceKey);

    // The provider client is owned by the composition root and may be shared.
    public void Dispose() { }

    private async Task<PreparedModelContext> PrepareAsync(
        IEnumerable<ChatMessage> messages,
        AttemptPreparationState? state,
        int? callNumber,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ChatMessage> originalMessages = messages as IReadOnlyList<ChatMessage> ?? [.. messages];
        ContextPreparationState automaticState = state?.AutomaticState ?? new ContextPreparationState();
        IReadOnlyList<ChatMessage> messagesToPrepare = state?.Checkpoint?.BuildInput(originalMessages) ?? originalMessages;
        int inputTokenBiasTokens = state?.InputTokenCalibration.BiasTokens ?? 0;
        IReadOnlyList<ChatMessage> compactedMessages = await _preparation
            .PrepareAsync(
                messagesToPrepare,
                session: null,
                unscopedState: automaticState,
                cancellationToken: cancellationToken,
                inputTokenBiasTokens: inputTokenBiasTokens)
            .ConfigureAwait(false);
        state?.UpdateCheckpoint(originalMessages, compactedMessages);
        int rawEstimateBefore = TokenEstimator.Estimate(originalMessages);
        int rawEstimate = TokenEstimator.Estimate(compactedMessages);
        int calibratedEstimate = AddTokenBias(rawEstimate, inputTokenBiasTokens);
        _logger.LogDebug(
            "Prepared per-call model context. GroupKey: {GroupKey}; AgentKey: {AgentKey}; AttemptId: {AttemptId}; CallNumber: {CallNumber}; RawEstimatedInputTokensBefore: {RawEstimatedInputTokensBefore}; RawEstimatedInputTokens: {RawEstimatedInputTokens}; InputTokenBiasTokens: {InputTokenBiasTokens}; CalibratedInputTokens: {CalibratedInputTokens}; AutomaticThreshold: {AutomaticThreshold}; WasCompacted: {WasCompacted}.",
            AgentRunAttemptContext.CurrentAgentGroupKey,
            AgentRunAttemptContext.CurrentAgentKey,
            AgentRunAttemptContext.CurrentAttemptId,
            callNumber,
            rawEstimateBefore,
            rawEstimate,
            inputTokenBiasTokens,
            calibratedEstimate,
            _options.Reducer.Options.GetAutoCompactThreshold(),
            compactedMessages.Any(message => message.AdditionalProperties?.ContainsKey(CompactionArtifactMetadata.IsCompactionSummaryKey) == true));
        return new PreparedModelContext(
            compactedMessages,
            new CallTokenPrediction(rawEstimate, calibratedEstimate));
    }

    private AttemptPreparationState? GetAttemptState() =>
        AgentRunAttemptContext.GetOrCreateAttemptState(_attemptStateKey, static () => new AttemptPreparationState());

    private void LogUsage(
        UsageDetails? usage,
        string? model,
        int? callNumber,
        CallTokenPrediction prediction,
        InputTokenCalibration? inputTokenCalibration)
    {
        if (usage is null)
            return;

        int? actualInputTokens = usage.InputTokenCount is { } inputTokenCount && inputTokenCount > 0
            ? (int)Math.Min(int.MaxValue, inputTokenCount)
            : null;
        InputTokenCalibrationObservation? calibrationObservation = actualInputTokens is { } inputTokens && inputTokenCalibration is not null
            ? inputTokenCalibration.Observe(
                prediction.RawEstimateTokens,
                prediction.CalibratedEstimateTokens,
                inputTokens)
            : null;

        _logger.LogDebug(
            "LLM usage received. GroupKey: {GroupKey}; AgentKey: {AgentKey}; AttemptId: {AttemptId}; CallNumber: {CallNumber}; Model: {Model}; RawEstimatedInputTokens: {RawEstimatedInputTokens}; CalibratedInputTokens: {CalibratedInputTokens}; InputTokens: {InputTokens}; PredictionErrorTokens: {PredictionErrorTokens}; ObservedBiasTokens: {ObservedBiasTokens}; InputTokenBiasTokens: {InputTokenBiasTokens}; CalibrationUpdated: {CalibrationUpdated}; OutputTokens: {OutputTokens}; TotalTokens: {TotalTokens}.",
            AgentRunAttemptContext.CurrentAgentGroupKey,
            AgentRunAttemptContext.CurrentAgentKey,
            AgentRunAttemptContext.CurrentAttemptId,
            callNumber,
            model,
            calibrationObservation?.RawEstimateTokens ?? prediction.RawEstimateTokens,
            calibrationObservation?.CalibratedEstimateTokens ?? prediction.CalibratedEstimateTokens,
            usage.InputTokenCount,
            calibrationObservation?.PredictionErrorTokens,
            calibrationObservation?.ObservedBiasTokens,
            calibrationObservation?.BiasTokens ?? inputTokenCalibration?.BiasTokens,
            calibrationObservation?.WasUpdated,
            usage.OutputTokenCount,
            usage.TotalTokenCount);
    }

    private sealed class AttemptPreparationState
    {
        public ContextPreparationState AutomaticState { get; } = new();
        public InputTokenCalibration InputTokenCalibration { get; } = new();
        public CompactionCheckpoint? Checkpoint { get; private set; }

        public void UpdateCheckpoint(IReadOnlyList<ChatMessage> rawMessages, IReadOnlyList<ChatMessage> preparedMessages)
        {
            if (preparedMessages.Any(message => message.AdditionalProperties?.ContainsKey(CompactionArtifactMetadata.IsCompactionSummaryKey) == true))
                Checkpoint = new CompactionCheckpoint(rawMessages, preparedMessages);
        }
    }

    private readonly record struct PreparedModelContext(
        IReadOnlyList<ChatMessage> Messages,
        CallTokenPrediction Prediction);

    private readonly record struct CallTokenPrediction(int RawEstimateTokens, int CalibratedEstimateTokens);

    private static int AddTokenBias(int rawEstimateTokens, int inputTokenBiasTokens) =>
        (int)Math.Min(int.MaxValue, Math.Max(0L, (long)rawEstimateTokens + inputTokenBiasTokens));

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
