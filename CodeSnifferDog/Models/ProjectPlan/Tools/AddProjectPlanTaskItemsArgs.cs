namespace CodeSnifferDog.Models.ProjectPlan.Tools;

public sealed class AddProjectPlanTaskItemsArgs
{
    public required IReadOnlyList<AddProjectPlanTaskItemArgs> TaskItems { get; init; }
}
