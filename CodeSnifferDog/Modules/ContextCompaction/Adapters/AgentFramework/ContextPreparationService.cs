using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Automatic;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

/// <summary>Shared execution path for the established context-preparation policy.</summary>
public sealed class ContextPreparationService
{
    private readonly CollapseController? _collapseController;
    private readonly MessageShrinker _messageShrinker;
    private readonly CompactionOptions _options;
    private readonly ChatReducer _reducer;
    private readonly ILogger _logger;
    private readonly AutomaticSessionState _sessionState = new();
    private readonly ContextPreparationState _fallbackState = new();

    public ContextPreparationService(AgentCompactionOptions agentOptions)
    {
        ArgumentNullException.ThrowIfNull(agentOptions);
        ArgumentNullException.ThrowIfNull(agentOptions.Reducer);
        _reducer = agentOptions.Reducer;
        _options = _reducer.Options;
        _messageShrinker = agentOptions.MessageShrinker ?? new MessageShrinker();
        _collapseController = agentOptions.CollapseController;
        _logger = agentOptions.LoggerFactory?.CreateLogger<ContextPreparationService>() ?? NullLogger<ContextPreparationService>.Instance;
    }

    /// <summary>Applies snip, micro-compaction, collapse, and automatic compaction without changing their rules.</summary>
    /// <param name="skipFullCompaction">Skips the reducer when a retry runtime already supplied a compacted transcript.</param>
    public async Task<IReadOnlyList<ChatMessage>> PrepareAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        ContextPreparationState? unscopedState = null,
        CancellationToken cancellationToken = default,
        int inputTokenAdjustmentTokens = 0,
        bool forceCompaction = false,
        int? precomputedRawEstimatedTokens = null,
        bool skipFullCompaction = false)
    {
        ArgumentNullException.ThrowIfNull(messages);
        IReadOnlyList<ChatMessage> requestMessages = messages as IReadOnlyList<ChatMessage> ?? [.. messages];
        int rawEstimatedTokens = precomputedRawEstimatedTokens ?? TokenEstimator.Estimate(requestMessages);
        _logger.LogDebug(
            "Preparing model context. Mode: {CompactionMode}; MessageCount: {MessageCount}; RawEstimatedTokens: {RawEstimatedTokens}; InputTokenAdjustmentTokens: {InputTokenAdjustmentTokens}; CalibratedEstimatedTokens: {CalibratedEstimatedTokens}; ForceCompaction: {ForceCompaction}; SkipFullCompaction: {SkipFullCompaction}; AutomaticThreshold: {AutomaticThreshold}; EffectiveContextWindowTokens: {EffectiveContextWindowTokens}.",
            _options.Mode,
            requestMessages.Count,
            rawEstimatedTokens,
            inputTokenAdjustmentTokens,
            TokenEstimator.ApplyTokenAdjustment(rawEstimatedTokens, inputTokenAdjustmentTokens),
            forceCompaction,
            skipFullCompaction,
            _options.GetAutoCompactThreshold(),
            _options.GetEffectiveContextWindowTokens());
        requestMessages = MessageShrinker.ApplySnip(requestMessages, _options).Messages;
        requestMessages = MessageShrinker.ApplyMicroCompaction(requestMessages, _options).Messages;
        if (skipFullCompaction)
            return requestMessages;

        if (_options.Mode == CompactionMode.ContextCollapse)
        {
            if (_collapseController is null) throw new InvalidOperationException("ContextCollapse mode requires an CollapseController.");
            try { return [.. await _collapseController.TryPrepareProactiveCollapseAsync(requestMessages, session, cancellationToken).ConfigureAwait(false)]; }
            catch (CompactionException exception)
            {
                _logger.LogWarning(exception, "Proactive context collapse failed. Falling back to prepared session messages.");
                return [.. _collapseController.PrepareMessages(requestMessages, session)];
            }
        }
        if (_options.Mode != CompactionMode.Standard ||
            (!_options.EnableAutomaticCompaction && !forceCompaction))
            return requestMessages;
        ContextPreparationState stateScope = unscopedState ?? _fallbackState;
        AutomaticCompactionState state = session is null ? stateScope.Get() : _sessionState.Get(session);
        if (state.CircuitBreakerOpen && !forceCompaction) return requestMessages;
        try
        {
            int automaticCompactionAdjustmentTokens = forceCompaction
                ? GetForcedCompactionAdjustmentTokens(requestMessages, inputTokenAdjustmentTokens)
                : inputTokenAdjustmentTokens;
            CompactionResult result = await _reducer
                .CompactAutomaticAsync(requestMessages, automaticCompactionAdjustmentTokens, cancellationToken)
                .ConfigureAwait(false);
            if (forceCompaction && !result.WasCompacted)
            {
                throw new CompactionException(
                    "Context recovery was required, but automatic compaction could not produce a replacement transcript.");
            }
            if (result.WasCompacted) { if (session is null) stateScope.Reset(); else _sessionState.Reset(session); }
            return ChatReducer.BuildMessages(result);
        }
        catch (CompactionException exception)
        {
            AutomaticCompactionState failureState;
            if (session is null)
            {
                failureState = stateScope.RecordFailure(_options);
            }
            else failureState = _sessionState.RecordFailure(session, _options);
            bool contextWindowExceeded = ModelInvocationFailureClassifier.IsContextWindowExceeded(exception);
            if (forceCompaction || contextWindowExceeded)
            {
                _logger.LogWarning(
                    exception,
                    "Automatic compaction failed while context recovery is required. Blocking the un-compacted context. ConsecutiveFailures: {ConsecutiveFailures}; CircuitBreakerOpen: {CircuitBreakerOpen}; ForceCompaction: {ForceCompaction}; ContextWindowExceeded: {ContextWindowExceeded}.",
                    failureState.ConsecutiveFailures,
                    failureState.CircuitBreakerOpen,
                    forceCompaction,
                    contextWindowExceeded);
                throw;
            }

            _logger.LogWarning(exception, "Automatic compaction failed. Returning un-compacted context. ConsecutiveFailures: {ConsecutiveFailures}; CircuitBreakerOpen: {CircuitBreakerOpen}.", failureState.ConsecutiveFailures, failureState.CircuitBreakerOpen);
            return requestMessages;
        }
    }

    private int GetForcedCompactionAdjustmentTokens(
        IReadOnlyList<ChatMessage> messages,
        int inputTokenAdjustmentTokens)
    {
        int rawEstimatedTokens = TokenEstimator.Estimate(messages);
        int requiredAdjustmentTokens = _options.GetAutoCompactThreshold() - rawEstimatedTokens;
        return Math.Max(inputTokenAdjustmentTokens, requiredAdjustmentTokens);
    }
}

/// <summary>Thread-safe automatic-compaction state for a non-agent-session invocation scope.</summary>
public sealed class ContextPreparationState
{
    private readonly object _syncRoot = new();
    private AutomaticCompactionState _automaticState = new();

    internal AutomaticCompactionState Get()
    {
        lock (_syncRoot)
            return _automaticState;
    }

    internal void Reset()
    {
        lock (_syncRoot)
            _automaticState = new AutomaticCompactionState();
    }

    internal AutomaticCompactionState RecordFailure(CompactionOptions options)
    {
        lock (_syncRoot)
        {
            int failures = _automaticState.ConsecutiveFailures + 1;
            return _automaticState = new AutomaticCompactionState
            {
                ConsecutiveFailures = failures,
                CircuitBreakerOpen = failures >= options.MaxConsecutiveAutomaticFailures,
            };
        }
    }
}
