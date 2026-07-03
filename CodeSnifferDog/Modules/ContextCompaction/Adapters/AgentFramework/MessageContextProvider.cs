using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Automatic;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public sealed class MessageContextProvider : MessageAIContextProvider
{
    private readonly CollapseController? _collapseController;
    private readonly MessageShrinker _messageShrinker;
    private readonly CompactionOptions _options;
    private readonly ChatReducer _reducer;
    private readonly AutomaticSessionState _sessionState = new();

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
