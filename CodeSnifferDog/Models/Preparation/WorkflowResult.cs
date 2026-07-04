using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;
using ProjectPlanWorkflowResult = CodeSnifferDog.Models.ProjectPlan.WorkflowResult;

namespace CodeSnifferDog.Models.Preparation;

/// <summary>
/// Holds the scan and project-plan outputs produced during repository preparation.
/// </summary>
public sealed class WorkflowResult
{
    /// <summary>
    /// Gets the scan workflow result for the repository.
    /// </summary>
    public required ScanWorkflowResult ScanResult { get; init; }

    /// <summary>
    /// Gets the project-plan workflow results produced for scanned projects.
    /// </summary>
    public required IReadOnlyList<ProjectPlanWorkflowResult> ProjectPlanResults { get; init; }
}
