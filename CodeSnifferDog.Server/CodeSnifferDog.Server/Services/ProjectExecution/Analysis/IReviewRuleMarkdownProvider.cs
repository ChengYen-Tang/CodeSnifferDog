namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

public interface IReviewRuleMarkdownProvider
{
    bool HasRules { get; }

    Task<IReadOnlyList<RuleDefinition>> LoadRulesAsync(CancellationToken cancellationToken = default);
}
