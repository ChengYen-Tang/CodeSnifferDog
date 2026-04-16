namespace CodeSnifferDog.Models.ProjectPlan;

public sealed class ProjectPlanTaskItem
{
    public required IReadOnlyList<ProjectPlanFile> Files { get; init; }
}
