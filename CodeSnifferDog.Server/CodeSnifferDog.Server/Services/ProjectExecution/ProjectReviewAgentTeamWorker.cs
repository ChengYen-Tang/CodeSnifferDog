using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

internal sealed class ProjectReviewAgentTeamWorker(ReviewAgentTeamWorker innerWorker) : IProjectReviewAgentTeamWorker
{
    private readonly ReviewAgentTeamWorker _innerWorker = innerWorker;

    public Task<ReviewAgentTeamAnalysisResult> AnalyzeDetailedAsync(CancellationToken cancellationToken = default) =>
        _innerWorker.AnalyzeDetailedAsync(cancellationToken);

    public ValueTask DisposeAsync() => _innerWorker.DisposeAsync();
}
