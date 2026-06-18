namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

public sealed class ProjectAnalysisContext
{
    public required Guid ProjectId { get; init; }

    public required string RepositoryRootPath { get; init; }
}
