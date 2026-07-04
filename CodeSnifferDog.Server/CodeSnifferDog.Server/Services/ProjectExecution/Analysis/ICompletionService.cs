using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

/// <summary>
/// Finalizes a completed analysis run by persisting reports and surfacing failures.
/// </summary>
internal interface ICompletionService
{
    /// <summary>
    /// Completes an analysis run for a project.
    /// </summary>
    /// <param name="projectId">Project identifier whose analysis completed.</param>
    /// <param name="rules">Rules that were used during analysis.</param>
    /// <param name="analysisResult">Detailed analysis result produced by the worker.</param>
    /// <param name="cancellationToken">Token that cancels the completion work.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the completion policy reports a failure or a rule report cannot be mapped back to a rule name.
    /// </exception>
    Task CompleteAnalysisAsync(
        Guid projectId,
        IReadOnlyList<RuleDefinition> rules,
        AnalysisResult analysisResult,
        CancellationToken cancellationToken = default);
}
