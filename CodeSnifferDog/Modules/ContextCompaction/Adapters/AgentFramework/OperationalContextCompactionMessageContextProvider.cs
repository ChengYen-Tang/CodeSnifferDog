using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public sealed class OperationalContextCompactionMessageContextProvider : MessageAIContextProvider
{
    private readonly OperationalContextCollapseController? _collapseController;
    private readonly OperationalContextMessageShrinker _messageShrinker;
    private readonly OperationalContextCompactionOptions _options;
    private readonly OperationalContextChatReducer _reducer;
    private readonly AutomaticCompactionSessionState _sessionState = new();

    public OperationalContextCompactionMessageContextProvider(
        OperationalContextAgentCompactionOptions agentOptions)
    {
        ArgumentNullException.ThrowIfNull(agentOptions);
        ArgumentNullException.ThrowIfNull(agentOptions.Reducer);

        _reducer = agentOptions.Reducer;
        _options = _reducer.Options;
        _messageShrinker = agentOptions.MessageShrinker ?? new OperationalContextMessageShrinker();
        _collapseController = agentOptions.CollapseController;
    }

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(
        InvokingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<ChatMessage> requestMessages = [.. context.RequestMessages];
        requestMessages = OperationalContextMessageShrinker.ApplySnip(requestMessages, _options).Messages;
        requestMessages = OperationalContextMessageShrinker.ApplyMicroCompaction(requestMessages, _options).Messages;

        if (_options.Mode == OperationalContextCompactionMode.ContextCollapse)
        {
            if (_collapseController is null)
                throw new InvalidOperationException("ContextCollapse mode requires an OperationalContextCollapseController.");

            try
            {
                requestMessages = await _collapseController.TryPrepareProactiveCollapseAsync(
                    requestMessages,
                    context.Session,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationalContextCompactionException)
            {
                return _collapseController.PrepareMessages(requestMessages, context.Session);
            }

            return requestMessages;
        }

        if (!_options.EnableAutomaticCompaction || _options.Mode != OperationalContextCompactionMode.Standard)
            return requestMessages;

        OperationalContextAutomaticCompactionState state = _sessionState.Get(context.Session);
        if (state.CircuitBreakerOpen)
            return requestMessages;

        try
        {
            OperationalContextCompactionResult result =
                await _reducer.CompactAutomaticAsync(requestMessages, cancellationToken).ConfigureAwait(false);

            if (result.WasCompacted)
                _sessionState.Reset(context.Session);

            return OperationalContextChatReducer.BuildMessages(result);
        }
        catch (OperationalContextCompactionException)
        {
            _sessionState.RecordFailure(context.Session, _options);
            return requestMessages;
        }
    }
}
