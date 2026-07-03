using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;
using ProjectPlanWorkflowResult = CodeSnifferDog.Models.ProjectPlan.WorkflowResult;

namespace CodeSnifferDog.Models.Preparation;

public sealed class WorkflowResult
{
    public required ScanWorkflowResult ScanResult { get; init; }

    public required IReadOnlyList<ProjectPlanWorkflowResult> ProjectPlanResults { get; init; }
}
