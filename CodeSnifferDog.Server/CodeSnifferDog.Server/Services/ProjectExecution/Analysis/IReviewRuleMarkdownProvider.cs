namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

public interface IReviewRuleMarkdownProvider
{
    bool HasRules { get; }

    Task<IReadOnlyList<ProjectExecutionRuleDefinition>> LoadRulesAsync(CancellationToken cancellationToken = default);
}
