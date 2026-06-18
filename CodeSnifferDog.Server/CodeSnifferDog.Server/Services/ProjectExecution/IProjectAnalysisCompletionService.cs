using CodeSnifferDog.Models.ReviewAgentTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

internal interface IProjectAnalysisCompletionService
{
    Task CompleteAnalysisAsync(
        Guid projectId,
        IReadOnlyList<ProjectExecutionRuleDefinition> rules,
        ReviewAgentTeamAnalysisResult analysisResult,
        CancellationToken cancellationToken = default);
}
