namespace CodeSnifferDog.Models.ProjectPlan.Tools;

public sealed class AddProjectPlanTaskItemsResult
{
    public required IReadOnlyList<string> ProjectPlanTaskItemIds { get; init; }
}
