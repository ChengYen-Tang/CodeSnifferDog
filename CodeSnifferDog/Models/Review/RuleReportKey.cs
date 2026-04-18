namespace CodeSnifferDog.Models.Review;

public readonly record struct RuleReportKey(
    string RepositoryRootPath,
    string RuleKey);
