namespace CodeSnifferDog.Models.ReviewStage;

public sealed class ProjectFlowResult
{
    public required IReadOnlyList<TaskItemFlowResult> TaskItemResults { get; init; }
}
