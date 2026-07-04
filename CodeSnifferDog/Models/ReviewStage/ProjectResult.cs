using CodeSnifferDog.Models.ReviewGroup;
using ProjectPlanWorkflowResult = CodeSnifferDog.Models.ProjectPlan.WorkflowResult;
using ReviewGroupWorkflowResult = CodeSnifferDog.Models.ReviewGroup.WorkflowResult;

namespace CodeSnifferDog.Models.ReviewStage;

/// <summary>
/// Holds the planning result and grouped review results for one project.
/// </summary>
public sealed class ProjectResult
{
    /// <summary>
    /// Gets the project-plan result that defined the review task items.
    /// </summary>
    public required ProjectPlanWorkflowResult ProjectPlanResult { get; init; }

    /// <summary>
    /// Gets the review-group results produced for the project's task items.
    /// </summary>
    public required IReadOnlyList<ReviewGroupWorkflowResult> ReviewGroupResults { get; init; }
}
