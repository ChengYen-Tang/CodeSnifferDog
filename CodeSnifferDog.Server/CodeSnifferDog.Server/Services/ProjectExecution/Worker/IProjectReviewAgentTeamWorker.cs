using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker;

internal interface IProjectReviewAgentTeamWorker : IAsyncDisposable
{
    Task<ReviewAgentTeamAnalysisResult> AnalyzeDetailedAsync(CancellationToken cancellationToken = default);
}
