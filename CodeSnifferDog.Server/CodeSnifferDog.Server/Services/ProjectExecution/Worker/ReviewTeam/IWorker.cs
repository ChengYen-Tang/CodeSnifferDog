using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

/// <summary>
/// Runs detailed project analysis through the review-team runtime.
/// </summary>
internal interface IWorker : IAsyncDisposable
{
    /// <summary>
    /// Runs detailed analysis and returns the complete analysis result.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the analysis.</param>
    /// <returns>The detailed analysis result.</returns>
    Task<AnalysisResult> AnalyzeDetailedAsync(CancellationToken cancellationToken = default);
}
