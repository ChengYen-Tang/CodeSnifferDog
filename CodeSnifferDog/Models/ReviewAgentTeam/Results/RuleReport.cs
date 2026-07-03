namespace CodeSnifferDog.Models.ReviewAgentTeam.Results;

public sealed class RuleReport
{
    public required string RuleKey { get; init; }

    public required string MarkdownContent { get; init; }
}
