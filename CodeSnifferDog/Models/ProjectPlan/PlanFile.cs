namespace CodeSnifferDog.Models.ProjectPlan;

public sealed class PlanFile
{
    public required string FilePath { get; init; }

    public required int TotalLines { get; init; }
}
