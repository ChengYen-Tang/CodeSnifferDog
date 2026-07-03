using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Server.Services.ProjectReports;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

internal sealed class CompletionService(IReportService projectReportService) : ICompletionService
{
    private readonly IReportService _projectReportService = projectReportService;

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
