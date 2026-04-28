namespace CodeSnifferDog.Server.Services.ProjectExecution;

public sealed class ProjectAnalysisContext
{
    public required Guid ProjectId { get; init; }

    public required string RepositoryRootPath { get; init; }
}
