namespace CodeSnifferDog.Models.ProjectPlan;

public sealed class TaskItem
{
    public required IReadOnlyList<PlanFile> Files { get; init; }
}
