using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

internal interface ICompletionService
{
    Task CompleteAnalysisAsync(
        Guid projectId,
        IReadOnlyList<RuleDefinition> rules,
        AnalysisResult analysisResult,
        CancellationToken cancellationToken = default);
}
