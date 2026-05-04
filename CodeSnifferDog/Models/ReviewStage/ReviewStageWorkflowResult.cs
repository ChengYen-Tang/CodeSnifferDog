namespace CodeSnifferDog.Models.ReviewStage;

public sealed class ReviewStageWorkflowResult
{
    public required IReadOnlyList<ReviewStageProjectResult> ProjectResults { get; init; }
}
