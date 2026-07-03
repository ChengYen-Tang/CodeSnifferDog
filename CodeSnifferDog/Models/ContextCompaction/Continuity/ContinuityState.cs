
namespace CodeSnifferDog.Models.ContextCompaction.Continuity;

public sealed class ContinuityState
{
    public string CurrentObjective { get; init; } = string.Empty;

    public string CompletedWork { get; init; } = string.Empty;

    public string NextSteps { get; init; } = string.Empty;

    public string CriticalContext { get; init; } = string.Empty;
}
