namespace CodeSnifferDog.Models.Review;

/// <summary>
/// Identifies one rule-report aggregation scope within a repository.
/// </summary>
/// <param name="RepositoryRootPath">Repository root used as the workflow scope.</param>
/// <param name="RuleKey">Rule key whose repository report is being aggregated.</param>
public readonly record struct RuleReportKey(
    string RepositoryRootPath,
    string RuleKey);
