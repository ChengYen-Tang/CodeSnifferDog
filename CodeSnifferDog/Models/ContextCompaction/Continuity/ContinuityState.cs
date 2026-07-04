
namespace CodeSnifferDog.Models.ContextCompaction.Continuity;

/// <summary>
/// Captures objective continuity that should survive transcript compaction.
/// </summary>
public sealed class ContinuityState
{
    /// <summary>
    /// Gets the current objective that remains active after compaction.
    /// </summary>
    public string CurrentObjective { get; init; } = string.Empty;

    /// <summary>
    /// Gets completed work that should be retained across compaction.
    /// </summary>
    public string CompletedWork { get; init; } = string.Empty;

    /// <summary>
    /// Gets next steps that should be retained across compaction.
    /// </summary>
    public string NextSteps { get; init; } = string.Empty;

    /// <summary>
    /// Gets critical context that should be retained across compaction.
    /// </summary>
    public string CriticalContext { get; init; } = string.Empty;
}
