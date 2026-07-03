using Microsoft.Agents.AI;
using CodeSnifferDog.Models.ContextCompaction.Automatic;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;

internal sealed class AutomaticSessionState
{
    private const string StateKey = "codesnifferdog.context_compaction.automatic_state";
    private readonly ProviderSessionState<AutomaticCompactionState> _sessionState =
        new(static _ => new AutomaticCompactionState(), StateKey);

    public AutomaticCompactionState Get(AgentSession? session) =>
        _sessionState.GetOrInitializeState(session);

    public void Reset(AgentSession? session) =>
        _sessionState.SaveState(session, new AutomaticCompactionState());

    public AutomaticCompactionState RecordFailure(
        AgentSession? session,
        CompactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        AutomaticCompactionState currentState = Get(session);
        int consecutiveFailures = currentState.ConsecutiveFailures + 1;
        AutomaticCompactionState nextState = new()
        {
            ConsecutiveFailures = consecutiveFailures,
            CircuitBreakerOpen = consecutiveFailures >= options.MaxConsecutiveAutomaticFailures,
        };

        _sessionState.SaveState(session, nextState);
        return nextState;
    }
}
