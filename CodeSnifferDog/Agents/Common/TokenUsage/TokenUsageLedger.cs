using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Agents.Common.TokenUsage;

/// <summary>
/// Keeps a provider-confirmed input-token checkpoint for one logical agent run.
/// </summary>
/// <remarks>
/// Provider usage counts include the fixed request overhead that local message estimates cannot see,
/// such as instructions and tool schemas. When a later request appends to the confirmed request,
/// this ledger uses the provider count as the base and estimates only the appended messages.
/// </remarks>
internal sealed class TokenUsageLedger
{
    private readonly object _syncRoot = new();
    private readonly InputTokenCalibration _fallbackCalibration = new();
    private readonly InputTokenCalibration _deltaCalibration = new();
    private TokenUsageCheckpoint? _checkpoint;
    private OutputUsageCheckpoint? _outputCheckpoint;
    private bool _requiresCompactionRecovery;

    /// <summary>
    /// Predicts the input-token count for a pending provider request.
    /// </summary>
    /// <param name="messages">Messages that will be sent to the provider.</param>
    /// <param name="modelId">Optional model identifier requested for the provider call.</param>
    /// <param name="requestFingerprint">Fingerprint of prompt and tool declarations that contribute provider-side input tokens.</param>
    /// <returns>The prediction and whether it is based on provider-confirmed usage.</returns>
    public TokenUsagePrediction CreatePrediction(
        IReadOnlyList<ChatMessage> messages,
        string? modelId = null,
        string? requestFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(messages);

        TokenUsageCheckpoint? checkpoint = null;
        OutputUsageCheckpoint? outputCheckpoint;
        int appendedStartIndex = 0;
        int deltaBiasTokens = 0;
        int fallbackBiasTokens = 0;
        bool requiresCompactionRecovery;

        lock (_syncRoot)
        {
            requiresCompactionRecovery = _requiresCompactionRecovery;
            outputCheckpoint = _outputCheckpoint;
            if (_checkpoint is { } candidate &&
                candidate.TryGetAppendedStartIndex(messages, modelId, requestFingerprint, out appendedStartIndex))
            {
                checkpoint = candidate;
                deltaBiasTokens = _deltaCalibration.BiasTokens;
            }
            else
                fallbackBiasTokens = _fallbackCalibration.BiasTokens;
        }

        if (checkpoint is null)
        {
            long fallbackRawEstimateBytes = TokenEstimator.GetByteCount(messages);
            int fallbackRawEstimateTokens = TokenEstimator.EstimateByteCount(fallbackRawEstimateBytes);
            OutputUsageAdjustment? fallbackOutputAdjustment = outputCheckpoint?.TryGetAdjustment(messages);
            int outputAwareEstimateTokens = fallbackOutputAdjustment is { } adjustment
                ? ReplaceTokens(fallbackRawEstimateTokens, adjustment.EstimatedOutputTokens, adjustment.ActualOutputTokens)
                : fallbackRawEstimateTokens;

            return new TokenUsagePrediction(
                fallbackRawEstimateBytes,
                fallbackRawEstimateTokens,
                AddTokens(outputAwareEstimateTokens, fallbackBiasTokens),
                UsesProviderCheckpoint: false,
                CheckpointInputTokens: null,
                DeltaEstimateTokens: null,
                DeltaBiasTokens: fallbackBiasTokens,
                RequiresCompactionRecovery: requiresCompactionRecovery,
                ReplayableOutputTokens: fallbackOutputAdjustment?.ActualOutputTokens,
                EstimatedReplayableOutputTokens: fallbackOutputAdjustment?.EstimatedOutputTokens);
        }

        // The checkpoint is immutable after publication. Estimate only its suffix outside the lock so the
        // serialization cost of a large tool result does not block another provider response.
        long deltaEstimateBytes = TokenEstimator.GetByteCount(messages, appendedStartIndex);
        long rawEstimateBytes = AddBytes(checkpoint.RawEstimateBytes, deltaEstimateBytes);
        int rawEstimateTokens = TokenEstimator.EstimateByteCount(rawEstimateBytes);
        int deltaEstimateTokens = appendedStartIndex == messages.Count
            ? 0
            : TokenEstimator.EstimateByteCount(deltaEstimateBytes);
        OutputUsageAdjustment? providerOutputAdjustment = outputCheckpoint?.TryGetAdjustment(messages, appendedStartIndex);
        if (providerOutputAdjustment is { } outputAdjustment)
            deltaEstimateTokens = outputAdjustment.AdjustedDeltaTokens;

        int checkpointBasedTokens = AddTokens(
            checkpoint.InputTokens,
            AddTokens(deltaEstimateTokens, deltaBiasTokens));

        return new TokenUsagePrediction(
            rawEstimateBytes,
            rawEstimateTokens,
            checkpointBasedTokens,
            UsesProviderCheckpoint: true,
            CheckpointInputTokens: checkpoint.InputTokens,
            DeltaEstimateTokens: deltaEstimateTokens,
            DeltaBiasTokens: deltaBiasTokens,
            RequiresCompactionRecovery: requiresCompactionRecovery,
            ReplayableOutputTokens: providerOutputAdjustment?.ActualOutputTokens,
            EstimatedReplayableOutputTokens: providerOutputAdjustment?.EstimatedOutputTokens);
    }

    /// <summary>
    /// Records the result of an accepted provider request and rebases the checkpoint when input usage is available.
    /// </summary>
    /// <param name="messages">The exact messages accepted by the provider.</param>
    /// <param name="prediction">Prediction made for the accepted request.</param>
    /// <param name="actualInputTokens">Provider-reported input tokens, when available.</param>
    /// <param name="modelId">Model identifier reported or requested for the call.</param>
    /// <param name="requestFingerprint">Fingerprint of prompt and tool declarations sent with the call.</param>
    /// <returns>The calibration observations made for this response.</returns>
    public TokenUsageLedgerObservation RecordSuccessfulResponse(
        IReadOnlyList<ChatMessage> messages,
        TokenUsagePrediction prediction,
        int? actualInputTokens,
        string? modelId = null,
        string? requestFingerprint = null,
        int? actualOutputTokens = null,
        int? replayableOutputTokens = null,
        IReadOnlyList<ChatMessage>? responseMessages = null)
    {
        ArgumentNullException.ThrowIfNull(messages);

        lock (_syncRoot)
        {
            InputTokenCalibrationObservation? fallbackObservation = null;
            InputTokenCalibrationObservation? deltaObservation = null;

            if (actualInputTokens is > 0)
            {
                if (prediction.UsesProviderCheckpoint &&
                    _checkpoint is { } checkpoint &&
                    checkpoint.TryGetAppendedStartIndex(messages, modelId, requestFingerprint, out _) &&
                    actualInputTokens.Value >= checkpoint.InputTokens &&
                    prediction.DeltaEstimateTokens is { } deltaEstimateTokens)
                {
                    int actualDeltaTokens = actualInputTokens.Value - checkpoint.InputTokens;
                    int calibratedDeltaTokens = AddTokens(deltaEstimateTokens, prediction.DeltaBiasTokens);
                    deltaObservation = _deltaCalibration.Observe(
                        deltaEstimateTokens,
                        calibratedDeltaTokens,
                        actualDeltaTokens);
                }
                else
                {
                    fallbackObservation = _fallbackCalibration.Observe(
                        prediction.RawEstimateTokens,
                        prediction.CalibratedEstimateTokens,
                        actualInputTokens);
                }

                _checkpoint = new TokenUsageCheckpoint(
                    messages,
                    actualInputTokens.Value,
                    modelId,
                    requestFingerprint,
                    prediction.RawEstimateBytes);
            }

            _outputCheckpoint = responseMessages is { Count: > 0 } && replayableOutputTokens is >= 0
                ? new OutputUsageCheckpoint(messages, responseMessages, replayableOutputTokens.Value)
                : null;

            // A provider response proves that the currently prepared request was accepted, so the next
            // request does not need overflow-only recovery even when this response omitted usage details.
            _requiresCompactionRecovery = false;

            return new TokenUsageLedgerObservation(
                actualInputTokens,
                fallbackObservation,
                deltaObservation,
                _fallbackCalibration.BiasTokens,
                _deltaCalibration.BiasTokens,
                actualOutputTokens,
                replayableOutputTokens);
        }
    }

    /// <summary>
    /// Gets the provider output-token count that can safely be associated with the replayed assistant messages.
    /// </summary>
    /// <remarks>
    /// Total output tokens may include hidden reasoning or audio. They are used for usage reporting, but are only
    /// used for the next prompt estimate when the provider exposes enough detail to identify replayable output.
    /// </remarks>
#pragma warning disable MEAI001
    public static int? GetReplayableOutputTokenCount(
        UsageDetails? usage,
        IReadOnlyList<ChatMessage>? responseMessages)
    {
        if (usage is null || responseMessages is not { Count: > 0 })
            return null;

        bool hasAssistantMessage = false;
        bool containsOnlyTextAssistantMessages = true;
        foreach (ChatMessage message in responseMessages)
        {
            if (message.Role != ChatRole.Assistant)
            {
                containsOnlyTextAssistantMessages = false;
                continue;
            }

            hasAssistantMessage = true;
            if (!IsTextOnlyAssistantMessage(message))
                containsOnlyTextAssistantMessages = false;
        }

        if (!hasAssistantMessage)
            return null;

        if (usage.OutputTokenCount is { } outputTokenCount &&
            usage.ReasoningTokenCount is { } reasoningTokenCount &&
            reasoningTokenCount >= 0 &&
            outputTokenCount >= reasoningTokenCount &&
            usage.OutputAudioTokenCount is not > 0)
        {
            return ClampTokenCount(outputTokenCount - reasoningTokenCount);
        }

        return containsOnlyTextAssistantMessages && usage.OutputTextTokenCount is { } outputTextTokenCount
            ? ClampTokenCount(outputTextTokenCount)
            : null;
    }
#pragma warning restore MEAI001

    /// <summary>
    /// Marks the current logical run so its next request forces compaction instead of replaying a context known to overflow.
    /// </summary>
    public void RecordContextWindowExceeded()
    {
        lock (_syncRoot)
            _requiresCompactionRecovery = true;
    }

    private static int AddTokens(int left, int right) =>
        (int)Math.Min(int.MaxValue, Math.Max(0L, (long)left + right));

    private static int ReplaceTokens(int totalTokens, int estimatedOutputTokens, int actualOutputTokens) =>
        (int)Math.Min(
            int.MaxValue,
            Math.Max(0L, (long)totalTokens - estimatedOutputTokens + actualOutputTokens));

    private static int? ClampTokenCount(long tokenCount) =>
        (int)Math.Min(int.MaxValue, Math.Max(0L, tokenCount));

    private static long AddBytes(long left, long right) =>
        right <= 0
            ? left
            : left >= long.MaxValue - right
                ? long.MaxValue
                : left + right;

    /// <summary>
    /// Captures the exact provider message sequence associated with one real input-token count.
    /// </summary>
    private sealed class TokenUsageCheckpoint(
        IReadOnlyList<ChatMessage> messages,
        int inputTokens,
        string? modelId,
        string? requestFingerprint,
        long rawEstimateBytes)
    {
        private readonly IReadOnlyList<ChatMessage> _messages = [.. messages];
        private readonly string? _modelId = modelId;
        private readonly string? _requestFingerprint = requestFingerprint;

        public int InputTokens { get; } = inputTokens;
        public long RawEstimateBytes { get; } = rawEstimateBytes;

        public bool TryGetAppendedStartIndex(
            IReadOnlyList<ChatMessage> currentMessages,
            string? currentModelId,
            string? currentRequestFingerprint,
            out int appendedStartIndex)
        {
            appendedStartIndex = 0;

            if (!IsCompatibleModel(currentModelId) ||
                !IsCompatibleRequest(currentRequestFingerprint) ||
                currentMessages.Count < _messages.Count)
                return false;

            for (int index = 0; index < _messages.Count; index++)
            {
                if (!ReferenceEquals(_messages[index], currentMessages[index]))
                    return false;
            }

            appendedStartIndex = _messages.Count;
            return true;
        }

        private bool IsCompatibleModel(string? currentModelId) =>
            _modelId is not null &&
            currentModelId is not null &&
            string.Equals(_modelId, currentModelId, StringComparison.Ordinal);

        private bool IsCompatibleRequest(string? currentRequestFingerprint) =>
            _requestFingerprint is not null &&
            currentRequestFingerprint is not null &&
            string.Equals(_requestFingerprint, currentRequestFingerprint, StringComparison.Ordinal);
    }

    private sealed class OutputUsageCheckpoint(
        IReadOnlyList<ChatMessage> requestMessages,
        IReadOnlyList<ChatMessage> responseMessages,
        int actualOutputTokens)
    {
        private readonly IReadOnlyList<ChatMessage> _requestMessages = [.. requestMessages];
        private readonly IReadOnlyList<ChatMessage> _responseMessages = [.. responseMessages];

        public OutputUsageAdjustment? TryGetAdjustment(
            IReadOnlyList<ChatMessage> currentMessages,
            int? expectedStartIndex = null)
        {
            int appendedStartIndex = expectedStartIndex ?? _requestMessages.Count;
            if (appendedStartIndex != _requestMessages.Count ||
                currentMessages.Count < appendedStartIndex + _responseMessages.Count)
            {
                return null;
            }

            for (int index = 0; index < _requestMessages.Count; index++)
            {
                if (!ReferenceEquals(_requestMessages[index], currentMessages[index]))
                    return null;
            }

            for (int index = 0; index < _responseMessages.Count; index++)
            {
                if (!ReferenceEquals(_responseMessages[index], currentMessages[appendedStartIndex + index]))
                    return null;
            }

            int estimatedOutputTokens = EstimateReplayableOutputMessages();
            int estimatedSuffixTokens = TokenEstimator.EstimateRange(currentMessages, appendedStartIndex);
            return new OutputUsageAdjustment(
                ReplaceTokens(estimatedSuffixTokens, estimatedOutputTokens, actualOutputTokens),
                estimatedOutputTokens,
                actualOutputTokens);
        }

        private int EstimateReplayableOutputMessages()
        {
            List<ChatMessage> assistantMessages = [];
            foreach (ChatMessage message in _responseMessages)
            {
                if (message.Role == ChatRole.Assistant)
                    assistantMessages.Add(message);
            }

            return assistantMessages.Count == 0
                ? 0
                : TokenEstimator.Estimate(assistantMessages);
        }
    }

    private static bool IsTextOnlyAssistantMessage(ChatMessage message) =>
        message.Role == ChatRole.Assistant &&
        !string.IsNullOrWhiteSpace(message.Text) &&
        message.Contents.All(static content => content is TextContent);
}

/// <summary>
/// Describes a pending provider request's token prediction.
/// </summary>
internal readonly record struct TokenUsagePrediction(
    long RawEstimateBytes,
    int RawEstimateTokens,
    int CalibratedEstimateTokens,
    bool UsesProviderCheckpoint,
    int? CheckpointInputTokens,
    int? DeltaEstimateTokens,
    int DeltaBiasTokens,
    bool RequiresCompactionRecovery,
    int? ReplayableOutputTokens = null,
    int? EstimatedReplayableOutputTokens = null)
{
    /// <summary>
    /// Gets the signed amount to apply to the local message estimate for threshold evaluation.
    /// </summary>
    /// <remarks>
    /// A negative value means a compatible provider-confirmed checkpoint is smaller than the local estimate.
    /// </remarks>
    public int InputTokenAdjustmentTokens => CalibratedEstimateTokens - RawEstimateTokens;
}

/// <summary>
/// Describes calibration updates made after one accepted provider request.
/// </summary>
internal readonly record struct TokenUsageLedgerObservation(
    int? ActualInputTokens,
    InputTokenCalibrationObservation? FallbackObservation,
    InputTokenCalibrationObservation? DeltaObservation,
    int FallbackBiasTokens,
    int DeltaBiasTokens,
    int? ActualOutputTokens,
    int? ReplayableOutputTokens);

/// <summary>
/// Describes the replacement of a local assistant-output estimate with provider-confirmed replayable tokens.
/// </summary>
internal readonly record struct OutputUsageAdjustment(
    int AdjustedDeltaTokens,
    int EstimatedOutputTokens,
    int ActualOutputTokens);
