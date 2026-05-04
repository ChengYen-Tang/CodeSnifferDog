namespace CodeSnifferDog.Models.ReviewStage;

public sealed class ReviewStageProjectFlowResult
{
    public required IReadOnlyList<ReviewStageTaskItemFlowResult> TaskItemResults { get; init; }
}
