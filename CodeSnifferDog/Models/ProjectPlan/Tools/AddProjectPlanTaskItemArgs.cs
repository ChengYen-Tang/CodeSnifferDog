namespace CodeSnifferDog.Models.ProjectPlan.Tools;

public sealed class AddProjectPlanTaskItemArgs
{
    public required IReadOnlyList<ProjectPlanFile> Files { get; init; }
}
