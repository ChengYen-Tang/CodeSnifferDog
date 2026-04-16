namespace CodeSnifferDog.Models.ProjectPlan;

public sealed class ProjectPlanFile
{
    public required string FilePath { get; init; }

    public required int TotalLines { get; init; }
}
