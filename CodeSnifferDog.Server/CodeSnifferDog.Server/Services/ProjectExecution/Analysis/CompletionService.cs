using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Server.Services.ProjectReports;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

/// <summary>
/// Persists analysis reports and translates completion policy failures into exceptions.
/// </summary>
internal sealed class CompletionService(IReportService projectReportService) : ICompletionService
{
    private readonly IReportService _projectReportService = projectReportService;

    /// <inheritdoc />
    public async Task CompleteAnalysisAsync(
        Guid projectId,
        IReadOnlyList<RuleDefinition> rules,
        AnalysisResult analysisResult,
        CancellationToken cancellationToken = default)
    {
        CompletionDecision completionDecision =
            CompletionPolicy.Evaluate(analysisResult);

        if (completionDecision.ShouldPersistReports)
            await PersistReportsAsync(projectId, rules, analysisResult.RuleReports, cancellationToken).ConfigureAwait(false);
        else
            await _projectReportService.ReplaceProjectReportsAsync(projectId, [], cancellationToken).ConfigureAwait(false);

        if (!completionDecision.IsSuccess)
            throw new InvalidOperationException(completionDecision.FailureMessage);
    }

    /// <summary>
    /// Persists rule reports with the human-readable rule names that belong to each report.
    /// </summary>
    /// <param name="projectId">Project identifier whose reports are being replaced.</param>
    /// <param name="rules">Rules that define the rule-key to rule-name mapping.</param>
    /// <param name="ruleReports">Rule reports produced by analysis.</param>
    /// <param name="cancellationToken">Token that cancels persistence.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a rule report references a rule key that is not present in <paramref name="rules"/>.
    /// </exception>
    private async Task PersistReportsAsync(
        Guid projectId,
        IReadOnlyList<RuleDefinition> rules,
        IReadOnlyList<RuleReport> ruleReports,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> ruleNamesByKey = rules.ToDictionary(rule => rule.RuleKey, rule => rule.RuleName, StringComparer.Ordinal);
        List<RuleDraft> drafts = [];
        foreach (RuleReport ruleReport in ruleReports)
        {
            if (!ruleNamesByKey.TryGetValue(ruleReport.RuleKey, out string? ruleName))
                throw new InvalidOperationException($"Rule name mapping was not found for rule key '{ruleReport.RuleKey}'.");

            drafts.Add(new RuleDraft
            {
                RuleKey = ruleReport.RuleKey,
                RuleName = ruleName,
                MarkdownContent = ruleReport.MarkdownContent,
            });
        }

        await _projectReportService.ReplaceProjectReportsAsync(projectId, drafts, cancellationToken);
    }
}
