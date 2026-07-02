using CodeSnifferDog.Models.ContextCompaction;
using Microsoft.Agents.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;

internal sealed class AutomaticCompactionSessionState
{
    private const string StateKey = "codesnifferdog.context_compaction.automatic_state";
    private readonly ProviderSessionState<OperationalContextAutomaticCompactionState> _sessionState =
        new(static _ => new OperationalContextAutomaticCompactionState(), StateKey);

    public OperationalContextAutomaticCompactionState Get(AgentSession? session) =>
        _sessionState.GetOrInitializeState(session);

    public void Reset(AgentSession? session) =>
        _sessionState.SaveState(session, new OperationalContextAutomaticCompactionState());

    public OperationalContextAutomaticCompactionState RecordFailure(
        AgentSession? session,
        OperationalContextCompactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        OperationalContextAutomaticCompactionState currentState = Get(session);
        int consecutiveFailures = currentState.ConsecutiveFailures + 1;
        OperationalContextAutomaticCompactionState nextState = new()
        {
            ConsecutiveFailures = consecutiveFailures,
            CircuitBreakerOpen = consecutiveFailures >= options.MaxConsecutiveAutomaticFailures,
        };

        _sessionState.SaveState(session, nextState);
        return nextState;
    }
}
