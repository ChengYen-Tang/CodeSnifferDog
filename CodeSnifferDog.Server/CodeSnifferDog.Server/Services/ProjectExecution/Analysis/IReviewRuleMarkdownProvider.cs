namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

/// <summary>
/// Loads markdown rule definitions used by project analysis.
/// </summary>
public interface IReviewRuleMarkdownProvider
{
    /// <summary>
    /// Gets a value indicating whether at least one non-empty rule markdown file is available.
    /// </summary>
    bool HasRules { get; }

    /// <summary>
    /// Loads all available rule definitions.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels file I/O.</param>
    /// <returns>The loaded rule definitions.</returns>
    Task<IReadOnlyList<RuleDefinition>> LoadRulesAsync(CancellationToken cancellationToken = default);
}
