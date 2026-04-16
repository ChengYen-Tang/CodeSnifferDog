namespace CodeSnifferDog.Models.ProjectPlan;

public sealed class StoredProjectPlanTaskItem
{
    public required string ProjectPlanTaskItemId { get; init; }

    public required IReadOnlyList<ProjectPlanFile> Files { get; init; }
}
