namespace CodeSnifferDog.Models.ReviewStage;

public sealed class WorkflowResult
{
    public required IReadOnlyList<ProjectResult> ProjectResults { get; init; }
}
