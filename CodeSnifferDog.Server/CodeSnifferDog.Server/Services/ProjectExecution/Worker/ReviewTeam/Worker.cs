using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using TeamWorker = CodeSnifferDog.Modules.ReviewAgentTeam.Runtime.Worker;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

internal sealed class Worker(TeamWorker innerWorker) : IWorker
{
    private readonly TeamWorker _innerWorker = innerWorker;

    public Task<AnalysisResult> AnalyzeDetailedAsync(CancellationToken cancellationToken = default) =>
        _innerWorker.AnalyzeDetailedAsync(cancellationToken);

    public ValueTask DisposeAsync() => _innerWorker.DisposeAsync();
}
