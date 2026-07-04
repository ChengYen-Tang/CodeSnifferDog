using CodeSnifferDog.Models.ReviewAgentTeam.Results;

namespace CodeSnifferDog.Models.ReviewAgentTeam.Analysis;

/// <summary>
/// Captures the outcome of the full review-agent analysis pipeline.
/// </summary>
public sealed class AnalysisResult
{
    /// <summary>
    /// Gets whether the preparation stage completed successfully.
    /// </summary>
    public required bool PreparationSucceeded { get; init; }

    /// <summary>
    /// Gets whether the review stage completed successfully.
    /// </summary>
    public required bool ReviewStageSucceeded { get; init; }

    /// <summary>
    /// Gets whether any rule flow produced findings.
    /// </summary>
    public required bool HasAnyFindings { get; init; }

    /// <summary>
    /// Gets whether every rule flow completed successfully.
    /// </summary>
    public required bool AllRuleFlowsSucceeded { get; init; }

    /// <summary>
    /// Gets execution errors collected while running the pipeline.
    /// </summary>
    public required IReadOnlyList<string> ExecutionErrors { get; init; }

    /// <summary>
    /// Gets rendered rule reports produced by the analysis.
    /// </summary>
    public required IReadOnlyList<RuleReport> RuleReports { get; init; }
}
