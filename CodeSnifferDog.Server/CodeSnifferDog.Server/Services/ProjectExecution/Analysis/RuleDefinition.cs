namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

public sealed class RuleDefinition
{
    public required string RuleKey { get; init; }

    public required string RuleName { get; init; }

    public required string RuleMarkdown { get; init; }
}
