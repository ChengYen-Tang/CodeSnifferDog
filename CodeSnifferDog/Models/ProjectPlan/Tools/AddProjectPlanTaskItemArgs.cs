namespace CodeSnifferDog.Models.ProjectPlan.Tools;

public sealed class AddProjectPlanTaskItemArgs
{
    public required IReadOnlyList<PlanFile> Files { get; init; }
}
