namespace CodeSnifferDog.Models.ReviewAgentTeam.Results;

/// <summary>
/// Holds the rendered markdown report for one reviewed rule.
/// </summary>
public sealed class RuleReport
{
    /// <summary>
    /// Gets the stable rule key.
    /// </summary>
    public required string RuleKey { get; init; }

    /// <summary>
    /// Gets the rendered markdown report content.
    /// </summary>
    public required string MarkdownContent { get; init; }
}
