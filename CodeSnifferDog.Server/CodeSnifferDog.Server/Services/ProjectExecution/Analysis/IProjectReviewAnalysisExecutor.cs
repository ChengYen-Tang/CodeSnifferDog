using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

internal interface IProjectReviewAnalysisExecutor
{
    Task<ReviewAgentTeamAnalysisResult> AnalyzeAsync(
        ProjectAnalysisContext context,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        CancellationToken cancellationToken = default);
}
