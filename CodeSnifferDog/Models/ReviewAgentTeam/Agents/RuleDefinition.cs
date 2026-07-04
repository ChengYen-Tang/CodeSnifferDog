namespace CodeSnifferDog.Models.ReviewAgentTeam.Agents;

/// <summary>
/// Describes one review rule that should be executed by the review-agent team.
/// </summary>
public sealed class RuleDefinition
{
    /// <summary>
    /// Gets the stable rule key.
    /// </summary>
    public required string RuleKey { get; init; }

    /// <summary>
    /// Gets the rendered markdown instructions for the rule.
    /// </summary>
    public required string RuleMarkdown { get; init; }
}
