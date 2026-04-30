namespace CodeSnifferDog.Server.Services.ProjectExecution;

public sealed class ProjectExecutionRuleDefinition
{
    public required string RuleKey { get; init; }

    public required string RuleName { get; init; }

    public required string RuleMarkdown { get; init; }
}
