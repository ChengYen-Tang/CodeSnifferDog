namespace CodeSnifferDog.Server.Services.ProjectExecution;

public interface IProjectAnalysisRunner
{
    bool IsReady { get; }

    Task RunAsync(ProjectAnalysisContext context, CancellationToken cancellationToken = default);
}
