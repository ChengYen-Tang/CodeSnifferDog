namespace CodeSnifferDog.Models.ReviewAgentTeam.Analysis;

/// <summary>
/// Evaluates an <see cref="AnalysisResult" /> into the final completion decision used by the runtime.
/// </summary>
public static class CompletionPolicy
{
    /// <summary>
    /// Evaluates whether the analysis should be considered successful and whether reports should be persisted.
    /// </summary>
    /// <param name="analysisResult">Analysis result to evaluate.</param>
    /// <returns>The evaluated completion decision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="analysisResult" /> is <see langword="null" />.</exception>
    public static CompletionDecision Evaluate(AnalysisResult analysisResult)
    {
        ArgumentNullException.ThrowIfNull(analysisResult);

        if (!analysisResult.PreparationSucceeded)
        {
            return new CompletionDecision
            {
                IsSuccess = false,
                ShouldPersistReports = false,
                FailureMessage = BuildFailureMessage(
                    "Project analysis could not start the review stage.",
                    analysisResult.ExecutionErrors),
            };
        }

        if (analysisResult.HasAnyFindings)
        {
            return new CompletionDecision
            {
                IsSuccess = true,
                ShouldPersistReports = true,
            };
        }

        if (analysisResult.ReviewStageSucceeded && analysisResult.AllRuleFlowsSucceeded)
        {
            return new CompletionDecision
            {
                IsSuccess = true,
                ShouldPersistReports = true,
            };
        }

        return new CompletionDecision
        {
            IsSuccess = false,
            ShouldPersistReports = false,
            FailureMessage = BuildFailureMessage(
                "Project analysis completed without findings, but one or more rule flows did not finish successfully.",
                analysisResult.ExecutionErrors),
        };
    }

    /// <summary>
    /// Builds a user-facing failure message from a summary and collected execution errors.
    /// </summary>
    /// <param name="summary">Summary line describing the failure mode.</param>
    /// <param name="executionErrors">Collected execution errors to append.</param>
    /// <returns>The formatted failure message.</returns>
    private static string BuildFailureMessage(string summary, IReadOnlyList<string> executionErrors)
    {
        if (executionErrors.Count == 0)
            return summary;

        return $"{summary}{Environment.NewLine}{string.Join(Environment.NewLine, executionErrors)}";
    }
}
