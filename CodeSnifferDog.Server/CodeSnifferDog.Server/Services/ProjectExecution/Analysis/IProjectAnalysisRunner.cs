namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

public interface IProjectAnalysisRunner
{
    bool IsReady { get; }

    Task RunAsync(ProjectAnalysisContext context, CancellationToken cancellationToken = default);
}
