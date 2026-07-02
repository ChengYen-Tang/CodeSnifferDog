using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

internal interface IWorker : IAsyncDisposable
{
    Task<ReviewAgentTeamAnalysisResult> AnalyzeDetailedAsync(CancellationToken cancellationToken = default);
}
