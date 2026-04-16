namespace CodeSnifferDog.Models.Preparation;

public sealed class RepositoryPreparationWorkflowOptions
{
    public int MaxConcurrentProjectPlans { get; init; } = 1;
}
