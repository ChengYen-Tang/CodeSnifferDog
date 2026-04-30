namespace CodeSnifferDog.Server.Services.ProjectExecution;

public interface IReviewRuleMarkdownProvider
{
    bool HasRules { get; }

    Task<IReadOnlyList<ProjectExecutionRuleDefinition>> LoadRulesAsync(CancellationToken cancellationToken = default);
}
