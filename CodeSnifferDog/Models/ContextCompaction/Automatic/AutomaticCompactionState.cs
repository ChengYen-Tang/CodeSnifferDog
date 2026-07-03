
namespace CodeSnifferDog.Models.ContextCompaction.Automatic;

public sealed class AutomaticCompactionState
{
    public int ConsecutiveFailures { get; init; }

    public bool CircuitBreakerOpen { get; init; }
}
