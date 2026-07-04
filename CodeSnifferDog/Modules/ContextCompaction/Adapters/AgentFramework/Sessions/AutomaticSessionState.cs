using Microsoft.Agents.AI;
using CodeSnifferDog.Models.ContextCompaction.Automatic;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;

/// <summary>
/// Persists per-session automatic-compaction circuit-breaker state.
/// </summary>
internal sealed class AutomaticSessionState
{
    private const string StateKey = "codesnifferdog.context_compaction.automatic_state";
    private readonly ProviderSessionState<AutomaticCompactionState> _sessionState =
        new(static _ => new AutomaticCompactionState(), StateKey);

    /// <summary>
    /// Gets the current automatic-compaction state for the supplied session, creating an empty state on first access.
    /// </summary>
    /// <param name="session">Session whose state should be loaded.</param>
    /// <returns>The current automatic-compaction state.</returns>
    public AutomaticCompactionState Get(AgentSession? session) =>
        _sessionState.GetOrInitializeState(session);

    /// <summary>
    /// Resets the automatic-compaction state for the supplied session.
    /// </summary>
    /// <param name="session">Session whose state should be cleared.</param>
    public void Reset(AgentSession? session) =>
        _sessionState.SaveState(session, new AutomaticCompactionState());

    /// <summary>
    /// Records one automatic-compaction failure and updates the circuit breaker.
    /// </summary>
    /// <param name="session">Session whose failure counter should be updated.</param>
    /// <param name="options">Compaction settings that define the maximum allowed consecutive automatic failures.</param>
    /// <returns>The updated automatic-compaction state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
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
