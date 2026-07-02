using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

internal interface IReviewAnalysisExecutor
{
    Task<ReviewAgentTeamAnalysisResult> AnalyzeAsync(
        ProjectAnalysisContext context,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        CancellationToken cancellationToken = default);
}
