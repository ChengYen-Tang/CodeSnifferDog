namespace CodeSnifferDog.Models.ReviewAgentTeam.Analysis;

public static class CompletionPolicy
{
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

    private static string BuildFailureMessage(string summary, IReadOnlyList<string> executionErrors)
    {
        if (executionErrors.Count == 0)
            return summary;

        return $"{summary}{Environment.NewLine}{string.Join(Environment.NewLine, executionErrors)}";
    }
}
