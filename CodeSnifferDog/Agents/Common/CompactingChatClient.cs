using CodeSnifferDog.Agents.Common.TokenUsage;
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
/// This adapter belongs below <see cref="FunctionInvokingChatClient" /> so each function-loop iteration
/// reaches the established compaction module without changing that module's behavior.
/// </remarks>
internal sealed class CompactingChatClient(
    IChatClient innerClient,
    AgentCompactionOptions options,
    ILoggerFactory? loggerFactory = null) : IChatClient
{
    private const string LogicalPreparationStateName = "context-preparation";

    private readonly IChatClient _innerClient = innerClient;
    private readonly AgentCompactionOptions _options = options;
    private readonly ContextPreparationService _preparation = new(options);
    private readonly object _attemptStateKey = new();
    private readonly ChatRequestFingerprintCache _requestFingerprintCache = new();
    private readonly ILogger _logger = loggerFactory?.CreateLogger("CodeSnifferDog.Agents.TokenUsage") ?? NullLogger.Instance;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        int? callNumber = AgentRunAttemptContext.GetNextModelCallNumber();
        PreparationState? state = GetPreparationState();
        string? requestedModelId = options?.ModelId ?? ChatClientIdentity.TryGetModelId(_innerClient);
        string? requestFingerprint = _requestFingerprintCache.Get(options);
        PreparedModelContext preparedContext;

        try
        {
            preparedContext = await PrepareAsync(
                messages,
                state,
                callNumber,
                requestedModelId,
                requestFingerprint,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ModelInvocationFailureClassifier.IsContextWindowExceeded(exception))
        {
            throw RecordContextWindowExceeded(exception, state);
        }

        try
        {
            ChatResponse response = await _innerClient
                .GetResponseAsync(preparedContext.Messages, options, cancellationToken)
                .ConfigureAwait(false);
            string? modelId = requestedModelId ?? response.ModelId;
            state?.UpdateCheckpoint(preparedContext.OriginalMessages, preparedContext.Messages);
            LogUsage(
                response.Usage,
                modelId,
                requestFingerprint,
                callNumber,
                preparedContext,
                state,
                [.. response.Messages]);
            return response;
        }
        catch (Exception exception) when (ModelInvocationFailureClassifier.IsContextWindowExceeded(exception))
        {
            throw RecordContextWindowExceeded(exception, state);
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int? callNumber = AgentRunAttemptContext.GetNextModelCallNumber();
        PreparationState? state = GetPreparationState();
        string? requestedModelId = options?.ModelId ?? ChatClientIdentity.TryGetModelId(_innerClient);
        string? requestFingerprint = _requestFingerprintCache.Get(options);
        PreparedModelContext preparedContext;

        try
        {
            preparedContext = await PrepareAsync(
                messages,
                state,
                callNumber,
                requestedModelId,
                requestFingerprint,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (ModelInvocationFailureClassifier.IsContextWindowExceeded(exception))
        {
            throw RecordContextWindowExceeded(exception, state);
        }

        string? responseModelId = requestedModelId;
        UsageDetails? completedUsage = null;
        List<ChatResponseUpdate> responseUpdates = [];

        IAsyncEnumerator<ChatResponseUpdate> enumerator;
        try
        {
            IAsyncEnumerable<ChatResponseUpdate> responseStream = _innerClient
                .GetStreamingResponseAsync(preparedContext.Messages, options, cancellationToken);
            enumerator = responseStream.GetAsyncEnumerator(cancellationToken);
        }
        catch (Exception exception) when (ModelInvocationFailureClassifier.IsContextWindowExceeded(exception))
        {
            throw RecordContextWindowExceeded(exception, state);
        }

        await using (enumerator)
        {
            while (true)
            {
                ChatResponseUpdate update;

                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                        break;

                    update = enumerator.Current;
                }
                catch (Exception exception) when (ModelInvocationFailureClassifier.IsContextWindowExceeded(exception))
                {
                    throw RecordContextWindowExceeded(exception, state);
                }

                responseModelId ??= update.ModelId;
                foreach (UsageContent usage in update.Contents.OfType<UsageContent>())
                {
                    completedUsage = MergeUsage(completedUsage, usage.Details);
                    LogUsage(
                        usage.Details,
                        responseModelId,
                        requestFingerprint,
                        callNumber,
                        preparedContext,
                        state,
                        commitLedger: false);
                }

                if (update.Contents.Any(static content => content is not UsageContent))
                {
                    ChatResponseUpdate contentUpdate = update.Clone();
                    contentUpdate.Contents = [.. update.Contents.Where(static content => content is not UsageContent)];
                    responseUpdates.Add(contentUpdate);
                }

                yield return update;
            }
        }

        state?.UpdateCheckpoint(preparedContext.OriginalMessages, preparedContext.Messages);
        IReadOnlyList<ChatMessage> responseMessages = BuildResponseMessages(responseUpdates);
        // Providers are allowed to omit usage for streaming responses. Completion still proves that the
        // prepared request was accepted and clears any overflow-only recovery requirement. Partial usage
        // updates above never commit this state, so an interrupted stream keeps recovery armed.
        LogUsage(
            completedUsage,
            responseModelId,
            requestFingerprint,
            callNumber,
            preparedContext,
            state,
            responseMessages);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        _innerClient.GetService(serviceType, serviceKey);

    // The provider client is owned by the composition root and may be shared.
    public void Dispose() { }

    private async Task<PreparedModelContext> PrepareAsync(
        IEnumerable<ChatMessage> messages,
        PreparationState? state,
        int? callNumber,
        string? modelId,
        string? requestFingerprint,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ChatMessage> originalMessages = messages as IReadOnlyList<ChatMessage> ?? [.. messages];
        ContextPreparationState automaticState = state?.AutomaticState ?? new ContextPreparationState();
        IReadOnlyList<ChatMessage> messagesToPrepare = state?.Checkpoint?.BuildInput(originalMessages) ?? originalMessages;
        bool skipFullCompaction = AgentRunAttemptContext.IsPreCompactedContext;
        TokenUsagePrediction preparationPrediction = state?.TokenUsageLedger.CreatePrediction(messagesToPrepare, modelId, requestFingerprint) ??
            CreateLocalPrediction(messagesToPrepare);

        IReadOnlyList<ChatMessage> compactedMessages = await _preparation
            .PrepareAsync(
                messagesToPrepare,
                session: null,
                unscopedState: automaticState,
                cancellationToken: cancellationToken,
                inputTokenAdjustmentTokens: preparationPrediction.InputTokenAdjustmentTokens,
                forceCompaction: preparationPrediction.RequiresCompactionRecovery && !skipFullCompaction,
                precomputedRawEstimatedTokens: preparationPrediction.RawEstimateTokens,
                skipFullCompaction: skipFullCompaction)
            .ConfigureAwait(false);

        TokenUsagePrediction providerPrediction = state?.TokenUsageLedger.CreatePrediction(compactedMessages, modelId, requestFingerprint) ??
            CreateLocalPrediction(compactedMessages);

        _logger.LogDebug(
            "Prepared per-call model context. GroupKey: {GroupKey}; AgentKey: {AgentKey}; AttemptId: {AttemptId}; CallNumber: {CallNumber}; RawEstimatedInputTokensBefore: {RawEstimatedInputTokensBefore}; RawEstimatedInputTokens: {RawEstimatedInputTokens}; InputTokenAdjustmentTokens: {InputTokenAdjustmentTokens}; CalibratedInputTokens: {CalibratedInputTokens}; UsesProviderCheckpoint: {UsesProviderCheckpoint}; CheckpointInputTokens: {CheckpointInputTokens}; DeltaEstimatedInputTokens: {DeltaEstimatedInputTokens}; DeltaBiasTokens: {DeltaBiasTokens}; ForceCompactionRecovery: {ForceCompactionRecovery}; SkipFullCompaction: {SkipFullCompaction}; AutomaticThreshold: {AutomaticThreshold}; WasCompacted: {WasCompacted}.",
            AgentRunAttemptContext.CurrentAgentGroupKey,
            AgentRunAttemptContext.CurrentAgentKey,
            AgentRunAttemptContext.CurrentAttemptId,
            callNumber,
            preparationPrediction.RawEstimateTokens,
            providerPrediction.RawEstimateTokens,
            providerPrediction.InputTokenAdjustmentTokens,
            providerPrediction.CalibratedEstimateTokens,
            providerPrediction.UsesProviderCheckpoint,
            providerPrediction.CheckpointInputTokens,
            providerPrediction.DeltaEstimateTokens,
            providerPrediction.DeltaBiasTokens,
            preparationPrediction.RequiresCompactionRecovery,
            skipFullCompaction,
            _options.Reducer.Options.GetAutoCompactThreshold(),
            compactedMessages.Any(message => message.AdditionalProperties?.ContainsKey(CompactionArtifactMetadata.IsCompactionSummaryKey) == true));

        return new PreparedModelContext(originalMessages, compactedMessages, providerPrediction);
    }

    private PreparationState? GetPreparationState() =>
        AgentRunAttemptContext.GetOrCreateLogicalRunState(
            LogicalPreparationStateName,
            static () => new PreparationState()) ??
        AgentRunAttemptContext.GetOrCreateAttemptState(
            _attemptStateKey,
            static () => new PreparationState());

    private void LogUsage(
        UsageDetails? usage,
        string? model,
        string? requestFingerprint,
        int? callNumber,
        PreparedModelContext preparedContext,
        PreparationState? state,
        IReadOnlyList<ChatMessage>? responseMessages = null,
        bool commitLedger = true)
    {
        int? actualInputTokens = usage?.InputTokenCount is { } inputTokenCount && inputTokenCount > 0
            ? (int)Math.Min(int.MaxValue, inputTokenCount)
            : null;
        int? actualOutputTokens = usage?.OutputTokenCount is { } outputTokenCount && outputTokenCount > 0
            ? (int)Math.Min(int.MaxValue, outputTokenCount)
            : null;
        int? replayableOutputTokens = TokenUsageLedger.GetReplayableOutputTokenCount(usage, responseMessages);
        TokenUsageLedgerObservation? ledgerObservation = commitLedger
            ? state?.TokenUsageLedger.RecordSuccessfulResponse(
                preparedContext.Messages,
                preparedContext.Prediction,
                actualInputTokens,
                model,
                requestFingerprint,
                actualOutputTokens,
                replayableOutputTokens,
                responseMessages)
            : null;

        if (usage is null)
            return;

        _logger.LogDebug(
            "LLM usage received. GroupKey: {GroupKey}; AgentKey: {AgentKey}; AttemptId: {AttemptId}; CallNumber: {CallNumber}; Model: {Model}; RawEstimatedInputTokens: {RawEstimatedInputTokens}; InputTokenAdjustmentTokens: {InputTokenAdjustmentTokens}; CalibratedInputTokens: {CalibratedInputTokens}; UsesProviderCheckpoint: {UsesProviderCheckpoint}; CheckpointInputTokens: {CheckpointInputTokens}; DeltaEstimatedInputTokens: {DeltaEstimatedInputTokens}; DeltaBiasTokens: {DeltaBiasTokens}; InputTokens: {InputTokens}; FallbackBiasTokens: {FallbackBiasTokens}; CalibrationUpdated: {CalibrationUpdated}; OutputTokens: {OutputTokens}; TotalTokens: {TotalTokens}.",
            AgentRunAttemptContext.CurrentAgentGroupKey,
            AgentRunAttemptContext.CurrentAgentKey,
            AgentRunAttemptContext.CurrentAttemptId,
            callNumber,
            model,
            preparedContext.Prediction.RawEstimateTokens,
            preparedContext.Prediction.InputTokenAdjustmentTokens,
            preparedContext.Prediction.CalibratedEstimateTokens,
            preparedContext.Prediction.UsesProviderCheckpoint,
            preparedContext.Prediction.CheckpointInputTokens,
            preparedContext.Prediction.DeltaEstimateTokens,
            ledgerObservation?.DeltaBiasTokens ?? preparedContext.Prediction.DeltaBiasTokens,
            usage.InputTokenCount,
            ledgerObservation?.FallbackBiasTokens,
            ledgerObservation?.FallbackObservation?.WasUpdated == true || ledgerObservation?.DeltaObservation?.WasUpdated == true,
            usage.OutputTokenCount,
            usage.TotalTokenCount);
    }

    private static UsageDetails MergeUsage(UsageDetails? previous, UsageDetails current) =>
        previous is null
            ? current
            : new UsageDetails
            {
                InputTokenCount = current.InputTokenCount ?? previous.InputTokenCount,
                OutputTokenCount = current.OutputTokenCount ?? previous.OutputTokenCount,
                TotalTokenCount = current.TotalTokenCount ?? previous.TotalTokenCount,
                CachedInputTokenCount = current.CachedInputTokenCount ?? previous.CachedInputTokenCount,
                ReasoningTokenCount = current.ReasoningTokenCount ?? previous.ReasoningTokenCount,
                AdditionalCounts = current.AdditionalCounts ?? previous.AdditionalCounts,
            };

    private static IReadOnlyList<ChatMessage> BuildResponseMessages(
        IReadOnlyList<ChatResponseUpdate> responseUpdates) =>
        responseUpdates.Count == 0
            ? []
            : [.. responseUpdates.ToChatResponse().Messages];

    private static ModelInvocationException RecordContextWindowExceeded(
        Exception exception,
        PreparationState? state)
    {
        state?.TokenUsageLedger.RecordContextWindowExceeded();
        return ModelInvocationFailureClassifier.NormalizeContextWindowExceeded(exception);
    }

    private static TokenUsagePrediction CreateLocalPrediction(IReadOnlyList<ChatMessage> messages)
    {
        long rawEstimateBytes = TokenEstimator.GetByteCount(messages);
        int rawEstimateTokens = TokenEstimator.EstimateByteCount(rawEstimateBytes);
        return new TokenUsagePrediction(
            rawEstimateBytes,
            rawEstimateTokens,
            rawEstimateTokens,
            UsesProviderCheckpoint: false,
            CheckpointInputTokens: null,
            DeltaEstimateTokens: null,
            DeltaBiasTokens: 0,
            RequiresCompactionRecovery: false);
    }

    private sealed class PreparationState
    {
        public ContextPreparationState AutomaticState { get; } = new();
        public TokenUsageLedger TokenUsageLedger { get; } = new();
        private CompactionCheckpoint? _checkpoint;

        public CompactionCheckpoint? Checkpoint => Volatile.Read(ref _checkpoint);

        public void UpdateCheckpoint(IReadOnlyList<ChatMessage> rawMessages, IReadOnlyList<ChatMessage> preparedMessages)
        {
            if (preparedMessages.Any(message => message.AdditionalProperties?.ContainsKey(CompactionArtifactMetadata.IsCompactionSummaryKey) == true))
                Volatile.Write(ref _checkpoint, new CompactionCheckpoint(rawMessages, preparedMessages));
        }
    }

    private readonly record struct PreparedModelContext(
        IReadOnlyList<ChatMessage> OriginalMessages,
        IReadOnlyList<ChatMessage> Messages,
        TokenUsagePrediction Prediction);

    private sealed class CompactionCheckpoint(IReadOnlyList<ChatMessage> rawMessages, IReadOnlyList<ChatMessage> preparedMessages)
    {
        private readonly IReadOnlyList<ChatMessage> _rawMessages = [.. rawMessages];
        private readonly IReadOnlyList<ChatMessage> _preparedMessages = [.. preparedMessages];

        public IReadOnlyList<ChatMessage>? BuildInput(IReadOnlyList<ChatMessage> currentRawMessages)
        {
            if (currentRawMessages.Count < _rawMessages.Count ||
                !_rawMessages.Select((message, index) => ReferenceEquals(message, currentRawMessages[index])).All(static matches => matches))
            {
                return null;
            }

            return [.. _preparedMessages, .. currentRawMessages.Skip(_rawMessages.Count)];
        }
    }
}
