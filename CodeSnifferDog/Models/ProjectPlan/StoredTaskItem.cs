namespace CodeSnifferDog.Models.ProjectPlan;

public sealed class StoredTaskItem
{
    public required string ProjectPlanTaskItemId { get; init; }

    public required IReadOnlyList<PlanFile> Files { get; init; }
}
