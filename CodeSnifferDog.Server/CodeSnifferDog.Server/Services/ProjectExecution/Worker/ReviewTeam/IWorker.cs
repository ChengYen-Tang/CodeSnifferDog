using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

internal interface IWorker : IAsyncDisposable
{
    Task<AnalysisResult> AnalyzeDetailedAsync(CancellationToken cancellationToken = default);
}
