using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Automatic;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

/// <summary>
/// Supplies request messages after applying local shrinking, proactive collapse, and optional automatic compaction.
/// </summary>
public sealed class MessageContextProvider : MessageAIContextProvider
{
    private readonly CollapseController? _collapseController;
    private readonly MessageShrinker _messageShrinker;
    private readonly CompactionOptions _options;
    private readonly ChatReducer _reducer;
    private readonly ILogger<MessageContextProvider> _logger;
    /// <summary>
    /// Tracks automatic-compaction circuit-breaker state across invocations for the current session.
    /// </summary>
    private readonly AutomaticSessionState _sessionState = new();

    /// <summary>
    /// Creates a context provider from agent-level compaction options.
    /// </summary>
    /// <param name="agentOptions">Agent compaction options that provide reducer, shrinker, and optional collapse controller dependencies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="agentOptions" /> or <paramref name="agentOptions.Reducer" /> is <see langword="null" />.</exception>
    public MessageContextProvider(
        AgentCompactionOptions agentOptions)
    {
        ArgumentNullException.ThrowIfNull(agentOptions);
        ArgumentNullException.ThrowIfNull(agentOptions.Reducer);

        _reducer = agentOptions.Reducer;
        _options = _reducer.Options;
        _messageShrinker = agentOptions.MessageShrinker ?? new MessageShrinker();
        _collapseController = agentOptions.CollapseController;
        _logger = agentOptions.LoggerFactory?.CreateLogger<MessageContextProvider>() ??
            NullLogger<MessageContextProvider>.Instance;
    }

    /// <summary>
    /// Produces the message list that should be sent to the model for the current invocation.
    /// </summary>
    /// <param name="context">Invocation context that supplies request messages and session state.</param>
    /// <param name="cancellationToken">Cancels proactive or automatic compaction work.</param>
    /// <returns>The prepared request messages.</returns>
    /// <remarks>
    /// Standard mode first applies snip and micro-compaction, then optionally performs automatic compaction unless the
    /// session circuit breaker is open. Context-collapse mode applies shrinking and proactive collapse projection instead.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Context-collapse mode is enabled but no collapse controller was configured.</exception>
    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(
        InvokingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<ChatMessage> requestMessages = [.. context.RequestMessages];
        int originalMessageCount = requestMessages.Count;
        int originalEstimatedTokens = TokenEstimator.Estimate(requestMessages);
        int automaticThreshold = _options.GetAutoCompactThreshold();

        _logger.LogDebug(
            "Preparing model context. Mode: {CompactionMode}; MessageCount: {MessageCount}; EstimatedTokens: {EstimatedTokens}; AutomaticThreshold: {AutomaticThreshold}; EffectiveContextWindowTokens: {EffectiveContextWindowTokens}.",
            _options.Mode,
            originalMessageCount,
            originalEstimatedTokens,
            automaticThreshold,
            _options.GetEffectiveContextWindowTokens());

        var snipResult = MessageShrinker.ApplySnip(requestMessages, _options);
        requestMessages = snipResult.Messages;
        if (snipResult.WasChanged)
        {
            _logger.LogDebug(
                "Context snip applied. OriginalMessageCount: {OriginalMessageCount}; MessageCount: {MessageCount}; FreedEstimatedTokens: {FreedEstimatedTokens}; ShrunkToolResultCount: {ShrunkToolResultCount}; EstimatedTokens: {EstimatedTokens}.",
                originalMessageCount,
                requestMessages.Count,
                snipResult.FreedEstimatedTokens,
                snipResult.ShrunkToolResultCount,
                TokenEstimator.Estimate(requestMessages));
        }

        var microCompactionResult = MessageShrinker.ApplyMicroCompaction(requestMessages, _options);
        requestMessages = microCompactionResult.Messages;
        if (microCompactionResult.WasChanged)
        {
            _logger.LogDebug(
                "Context micro-compaction applied. MessageCount: {MessageCount}; FreedEstimatedTokens: {FreedEstimatedTokens}; ShrunkToolResultCount: {ShrunkToolResultCount}; EstimatedTokens: {EstimatedTokens}.",
                requestMessages.Count,
                microCompactionResult.FreedEstimatedTokens,
                microCompactionResult.ShrunkToolResultCount,
                TokenEstimator.Estimate(requestMessages));
        }

        if (_options.Mode == CompactionMode.ContextCollapse)
        {
            if (_collapseController is null)
                throw new InvalidOperationException("ContextCollapse mode requires an CollapseController.");

            try
            {
                _logger.LogDebug(
                    "Preparing proactive context collapse. MessageCount: {MessageCount}; EstimatedTokens: {EstimatedTokens}.",
                    requestMessages.Count,
                    TokenEstimator.Estimate(requestMessages));

                requestMessages = await _collapseController.TryPrepareProactiveCollapseAsync(
                    requestMessages,
                    context.Session,
                    cancellationToken).ConfigureAwait(false);

                _logger.LogDebug(
                    "Proactive context collapse prepared. MessageCount: {MessageCount}; EstimatedTokens: {EstimatedTokens}.",
                    requestMessages.Count,
                    TokenEstimator.Estimate(requestMessages));
            }
            catch (CompactionException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Proactive context collapse failed. Falling back to prepared session messages. MessageCount: {MessageCount}; EstimatedTokens: {EstimatedTokens}.",
                    requestMessages.Count,
                    TokenEstimator.Estimate(requestMessages));

                return _collapseController.PrepareMessages(requestMessages, context.Session);
            }

            return requestMessages;
        }

        if (!_options.EnableAutomaticCompaction || _options.Mode != CompactionMode.Standard)
        {
            _logger.LogDebug(
                "Automatic compaction skipped. Enabled: {AutomaticCompactionEnabled}; Mode: {CompactionMode}; MessageCount: {MessageCount}; EstimatedTokens: {EstimatedTokens}.",
                _options.EnableAutomaticCompaction,
                _options.Mode,
                requestMessages.Count,
                TokenEstimator.Estimate(requestMessages));

            return requestMessages;
        }

        AutomaticCompactionState state = _sessionState.Get(context.Session);
        if (state.CircuitBreakerOpen)
        {
            _logger.LogWarning(
                "Automatic compaction circuit breaker is open. Returning un-compacted context. ConsecutiveFailures: {ConsecutiveFailures}; MessageCount: {MessageCount}; EstimatedTokens: {EstimatedTokens}; AutomaticThreshold: {AutomaticThreshold}.",
                state.ConsecutiveFailures,
                requestMessages.Count,
                TokenEstimator.Estimate(requestMessages),
                automaticThreshold);

            return requestMessages;
        }

        try
        {
            int beforeAutomaticEstimatedTokens = TokenEstimator.Estimate(requestMessages);
            _logger.LogDebug(
                "Automatic compaction evaluation started. MessageCount: {MessageCount}; EstimatedTokens: {EstimatedTokens}; AutomaticThreshold: {AutomaticThreshold}; ConsecutiveFailures: {ConsecutiveFailures}.",
                requestMessages.Count,
                beforeAutomaticEstimatedTokens,
                automaticThreshold,
                state.ConsecutiveFailures);

            CompactionResult result =
                await _reducer.CompactAutomaticAsync(requestMessages, cancellationToken).ConfigureAwait(false);

            if (result.WasCompacted)
                _sessionState.Reset(context.Session);

            IReadOnlyList<ChatMessage> compactedMessages = ChatReducer.BuildMessages(result);
            _logger.LogDebug(
                "Automatic compaction evaluation completed. WasCompacted: {WasCompacted}; OriginalMessageCount: {OriginalMessageCount}; MessageCount: {MessageCount}; EstimatedTokensBefore: {EstimatedTokensBefore}; EstimatedTokensAfter: {EstimatedTokensAfter}; ArchivedMessageCount: {ArchivedMessageCount}.",
                result.WasCompacted,
                requestMessages.Count,
                compactedMessages.Count,
                beforeAutomaticEstimatedTokens,
                TokenEstimator.Estimate(compactedMessages),
                result.ArchivedMessageReferences.Count);

            return compactedMessages;
        }
        catch (CompactionException exception)
        {
            AutomaticCompactionState failureState = _sessionState.RecordFailure(context.Session, _options);
            _logger.LogWarning(
                exception,
                "Automatic compaction failed. Returning un-compacted context. ConsecutiveFailures: {ConsecutiveFailures}; CircuitBreakerOpen: {CircuitBreakerOpen}; MessageCount: {MessageCount}; EstimatedTokens: {EstimatedTokens}; AutomaticThreshold: {AutomaticThreshold}.",
                failureState.ConsecutiveFailures,
                failureState.CircuitBreakerOpen,
                requestMessages.Count,
                TokenEstimator.Estimate(requestMessages),
                automaticThreshold);

            return requestMessages;
        }
    }
}
