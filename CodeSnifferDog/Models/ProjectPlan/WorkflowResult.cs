using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Models.ProjectPlan;

/// <summary>
/// Holds the outputs and execution metadata produced by one project-plan workflow run.
/// </summary>
public sealed class WorkflowResult
{
    /// <summary>
    /// Gets the scanned project that was planned.
    /// </summary>
    public required StoredScanProject ScanProject { get; init; }

    /// <summary>
    /// Gets the task items generated for the project.
    /// </summary>
    public required IReadOnlyList<StoredTaskItem> TaskItems { get; init; }

    /// <summary>
    /// Gets the final review verdict for the planning output.
    /// </summary>
    public required ReviewVerdict Verdict { get; init; }

    /// <summary>
    /// Gets whether the workflow continued after exhausting verifier rejection attempts.
    /// </summary>
    public required bool ContinuedAfterVerifierRejectionLimit { get; init; }

    /// <summary>
    /// Gets how many planning-agent attempts were executed.
    /// </summary>
    public required int PlanAttempts { get; init; }

    /// <summary>
    /// Gets how many verifier attempts were executed.
    /// </summary>
    public required int VerifierAttempts { get; init; }

    /// <summary>
    /// Gets how many times the project-plan agent was reset.
    /// </summary>
    public required int ProjectPlanAgentResetCount { get; init; }
}
