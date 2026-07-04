namespace CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

/// <summary>
/// Loads rule markdown files from the application's <c>rules</c> directory.
/// </summary>
public sealed class FileSystemRuleMarkdownProvider : IReviewRuleMarkdownProvider
{
    private const string RulesDirectoryName = "rules";

    /// <inheritdoc />
    public bool HasRules =>
        Directory.Exists(ResolveRulesDirectoryPath()) &&
        Directory.EnumerateFiles(ResolveRulesDirectoryPath(), "*.md", SearchOption.TopDirectoryOnly)
            .Any(ruleFilePath => !string.IsNullOrWhiteSpace(File.ReadAllText(ruleFilePath)));

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuleDefinition>> LoadRulesAsync(CancellationToken cancellationToken = default)
    {
        string rulesDirectoryPath = ResolveRulesDirectoryPath();
        if (!Directory.Exists(rulesDirectoryPath))
            return [];

        List<RuleDefinition> rules = [];
        foreach (string ruleFilePath in Directory
            .EnumerateFiles(rulesDirectoryPath, "*.md", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase))
        {
            string ruleMarkdown = await File.ReadAllTextAsync(ruleFilePath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(ruleMarkdown))
            {
                string ruleName = Path.GetFileNameWithoutExtension(ruleFilePath);
                rules.Add(new RuleDefinition
                {
                    RuleKey = ruleName,
                    RuleName = ruleName,
                    RuleMarkdown = ruleMarkdown,
                });
            }
        }

        return rules;
    }

    /// <summary>
    /// Resolves the absolute path of the directory that stores rule markdown files.
    /// </summary>
    /// <returns>The absolute rules directory path.</returns>
    private string ResolveRulesDirectoryPath()
        => Path.Combine(AppContext.BaseDirectory, RulesDirectoryName);
}
