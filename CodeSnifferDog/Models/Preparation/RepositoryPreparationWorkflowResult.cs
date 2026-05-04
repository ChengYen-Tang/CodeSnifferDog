using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Models.Preparation;

public sealed class RepositoryPreparationWorkflowResult
{
    public required ScanWorkflowResult ScanResult { get; init; }

    public required IReadOnlyList<ProjectPlanWorkflowResult> ProjectPlanResults { get; init; }
}
