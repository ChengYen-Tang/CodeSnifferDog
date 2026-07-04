using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

/// <summary>
/// Executes the review-agent analysis workflow for a project.
/// </summary>
internal interface IReviewAnalysisExecutor
{
    /// <summary>
    /// Runs review analysis for the specified project and rules.
    /// </summary>
    /// <param name="context">Project analysis context.</param>
    /// <param name="rules">Rules that should be evaluated.</param>
    /// <param name="cancellationToken">Token that cancels the analysis.</param>
    /// <returns>The detailed analysis result.</returns>
    Task<AnalysisResult> AnalyzeAsync(
        ProjectAnalysisContext context,
        IReadOnlyList<RuleDefinition> rules,
        CancellationToken cancellationToken = default);
}
