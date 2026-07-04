using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Modules.Tools.Issues;

/// <summary>
/// Wraps a normalized review issue so stores can compare or map it consistently.
/// </summary>
/// <param name="Issue">Normalized issue payload.</param>
internal sealed record NormalizedRuleIssue(Issue Issue);
