namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextAutomaticCompactionState
{
    public int ConsecutiveFailures { get; init; }

    public bool CircuitBreakerOpen { get; init; }
}
