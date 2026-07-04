namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

/// <summary>
/// Runs the end-to-end project analysis pipeline.
/// </summary>
public interface IProjectAnalysisRunner
{
    /// <summary>
    /// Gets a value indicating whether the analysis runner has the dependencies required to execute.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Runs the analysis pipeline for a project.
    /// </summary>
    /// <param name="context">Project analysis context.</param>
    /// <param name="cancellationToken">Token that cancels the analysis.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the runner is not ready, when configuration is invalid, or when no rules are available.
    /// </exception>
    Task RunAsync(ProjectAnalysisContext context, CancellationToken cancellationToken = default);
}
