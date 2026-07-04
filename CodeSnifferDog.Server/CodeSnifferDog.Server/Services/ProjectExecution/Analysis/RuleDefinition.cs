namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

/// <summary>
/// Represents a markdown rule definition used during project analysis.
/// </summary>
public sealed class RuleDefinition
{
    /// <summary>
    /// Gets the stable rule key.
    /// </summary>
    public required string RuleKey { get; init; }

    /// <summary>
    /// Gets the human-readable rule name.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// Gets the markdown content supplied to the review workflow.
    /// </summary>
    public required string RuleMarkdown { get; init; }
}
