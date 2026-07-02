using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam.Runtime;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

internal sealed class Worker(ReviewAgentTeamWorker innerWorker) : IWorker
{
    private readonly ReviewAgentTeamWorker _innerWorker = innerWorker;

    public Task<ReviewAgentTeamAnalysisResult> AnalyzeDetailedAsync(CancellationToken cancellationToken = default) =>
        _innerWorker.AnalyzeDetailedAsync(cancellationToken);

    public ValueTask DisposeAsync() => _innerWorker.DisposeAsync();
}
