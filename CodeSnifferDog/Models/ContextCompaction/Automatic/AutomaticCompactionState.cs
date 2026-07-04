
namespace CodeSnifferDog.Models.ContextCompaction.Automatic;

/// <summary>
/// Tracks automatic-compaction failure state and circuit-breaker status.
/// </summary>
public sealed class AutomaticCompactionState
{
    /// <summary>
    /// Gets the current number of consecutive automatic-compaction failures.
    /// </summary>
    public int ConsecutiveFailures { get; init; }

    /// <summary>
    /// Gets whether the automatic-compaction circuit breaker is open.
    /// </summary>
    public bool CircuitBreakerOpen { get; init; }
}
