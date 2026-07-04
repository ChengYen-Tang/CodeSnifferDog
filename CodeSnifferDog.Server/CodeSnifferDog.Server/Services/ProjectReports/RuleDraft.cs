namespace CodeSnifferDog.Server.Services.ProjectReports;

/// <summary>
/// Represents one generated rule report before it is persisted.
/// </summary>
public sealed class RuleDraft
{
    private string _ruleKey = string.Empty;
    private string _ruleName = string.Empty;
    private string _markdownContent = string.Empty;

    /// <summary>
    /// Gets the stable rule key.
    /// </summary>
    /// <exception cref="ArgumentException">The assigned value is null, empty, or whitespace.</exception>
    public required string RuleKey
    {
        get => _ruleKey;
        init => _ruleKey = ValidateRequiredText(value, nameof(RuleKey));
    }

    /// <summary>
    /// Gets the human-readable rule name.
    /// </summary>
    /// <exception cref="ArgumentException">The assigned value is null, empty, or whitespace.</exception>
    public required string RuleName
    {
        get => _ruleName;
        init => _ruleName = ValidateRequiredText(value, nameof(RuleName));
    }

    /// <summary>
    /// Gets the rendered markdown report content.
    /// </summary>
    /// <exception cref="ArgumentException">The assigned value is null, empty, or whitespace.</exception>
    public required string MarkdownContent
    {
        get => _markdownContent;
        init => _markdownContent = ValidateRequiredText(value, nameof(MarkdownContent));
    }

    /// <summary>
    /// Validates required text for persisted draft properties.
    /// </summary>
    /// <param name="value">Candidate text.</param>
    /// <param name="propertyName">Property name used in the thrown exception when validation fails.</param>
    /// <returns>The validated text.</returns>
    /// <exception cref="ArgumentException"><paramref name="value" /> is null, empty, or whitespace.</exception>
    private static string ValidateRequiredText(string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{propertyName} cannot be null, empty, or whitespace.", propertyName);

        return value;
    }
}
