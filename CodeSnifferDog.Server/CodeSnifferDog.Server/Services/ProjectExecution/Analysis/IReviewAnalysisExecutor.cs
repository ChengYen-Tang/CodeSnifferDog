using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

internal interface IReviewAnalysisExecutor
{
    Task<AnalysisResult> AnalyzeAsync(
        ProjectAnalysisContext context,
        IReadOnlyList<RuleDefinition> rules,
        CancellationToken cancellationToken = default);
}
