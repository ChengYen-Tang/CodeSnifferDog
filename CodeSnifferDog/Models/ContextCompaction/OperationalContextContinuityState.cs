namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextContinuityState
{
    public string CurrentObjective { get; init; } = string.Empty;

    public string CompletedWork { get; init; } = string.Empty;

    public string NextSteps { get; init; } = string.Empty;

    public string CriticalContext { get; init; } = string.Empty;
}
