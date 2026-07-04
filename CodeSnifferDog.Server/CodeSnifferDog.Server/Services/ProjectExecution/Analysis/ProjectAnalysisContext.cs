namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

/// <summary>
/// Carries the project-specific inputs required to run analysis.
/// </summary>
public sealed class ProjectAnalysisContext
{
    /// <summary>
    /// Gets the project identifier.
    /// </summary>
    public required Guid ProjectId { get; init; }

    /// <summary>
    /// Gets the repository root path that should be analyzed.
    /// </summary>
    public required string RepositoryRootPath { get; init; }
}
