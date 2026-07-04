using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Automatic;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

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
        requestMessages = MessageShrinker.ApplySnip(requestMessages, _options).Messages;
        requestMessages = MessageShrinker.ApplyMicroCompaction(requestMessages, _options).Messages;

        if (_options.Mode == CompactionMode.ContextCollapse)
        {
            if (_collapseController is null)
                throw new InvalidOperationException("ContextCollapse mode requires an CollapseController.");

            try
            {
                requestMessages = await _collapseController.TryPrepareProactiveCollapseAsync(
                    requestMessages,
                    context.Session,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (CompactionException)
            {
                return _collapseController.PrepareMessages(requestMessages, context.Session);
            }

            return requestMessages;
        }

        if (!_options.EnableAutomaticCompaction || _options.Mode != CompactionMode.Standard)
            return requestMessages;

        AutomaticCompactionState state = _sessionState.Get(context.Session);
        if (state.CircuitBreakerOpen)
            return requestMessages;

        try
        {
            CompactionResult result =
                await _reducer.CompactAutomaticAsync(requestMessages, cancellationToken).ConfigureAwait(false);

            if (result.WasCompacted)
                _sessionState.Reset(context.Session);

            return ChatReducer.BuildMessages(result);
        }
        catch (CompactionException)
        {
            _sessionState.RecordFailure(context.Session, _options);
            return requestMessages;
        }
    }
}
