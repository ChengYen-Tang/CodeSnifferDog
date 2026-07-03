namespace CodeSnifferDog.Server.Services.ProjectReports;

public sealed class RuleDraft
{
    private string _ruleKey = string.Empty;
    private string _ruleName = string.Empty;
    private string _markdownContent = string.Empty;

    public required string RuleKey
    {
        get => _ruleKey;
        init => _ruleKey = ValidateRequiredText(value, nameof(RuleKey));
    }

    public required string RuleName
    {
        get => _ruleName;
        init => _ruleName = ValidateRequiredText(value, nameof(RuleName));
    }

    public required string MarkdownContent
    {
        get => _markdownContent;
        init => _markdownContent = ValidateRequiredText(value, nameof(MarkdownContent));
    }

    private static string ValidateRequiredText(string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{propertyName} cannot be null, empty, or whitespace.", propertyName);

        return value;
    }
}
