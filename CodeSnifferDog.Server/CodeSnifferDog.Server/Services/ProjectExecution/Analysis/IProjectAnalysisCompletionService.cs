using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

internal interface IProjectAnalysisCompletionService
{
    Task CompleteAnalysisAsync(
        Guid projectId,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        ReviewAgentTeamAnalysisResult analysisResult,
        CancellationToken cancellationToken = default);
}
