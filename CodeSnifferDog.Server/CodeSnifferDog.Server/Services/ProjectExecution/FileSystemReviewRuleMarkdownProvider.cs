namespace CodeSnifferDog.Server.Services.ProjectExecution;

public sealed class FileSystemReviewRuleMarkdownProvider : IReviewRuleMarkdownProvider
{
    private const string RulesDirectoryName = "rules";

    public bool HasRules =>
        Directory.Exists(ResolveRulesDirectoryPath()) &&
        Directory.EnumerateFiles(ResolveRulesDirectoryPath(), "*.md", SearchOption.TopDirectoryOnly)
            .Any(ruleFilePath => !string.IsNullOrWhiteSpace(File.ReadAllText(ruleFilePath)));

    public async Task<IReadOnlyList<string>> LoadRuleMarkdownsAsync(CancellationToken cancellationToken = default)
    {
        string rulesDirectoryPath = ResolveRulesDirectoryPath();
        if (!Directory.Exists(rulesDirectoryPath))
            return [];

        List<string> ruleMarkdowns = [];
        foreach (string ruleFilePath in Directory
            .EnumerateFiles(rulesDirectoryPath, "*.md", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.OrdinalIgnoreCase))
        {
            string ruleMarkdown = await File.ReadAllTextAsync(ruleFilePath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(ruleMarkdown))
                ruleMarkdowns.Add(ruleMarkdown);
        }

        return ruleMarkdowns;
    }

    private string ResolveRulesDirectoryPath()
        => Path.Combine(AppContext.BaseDirectory, RulesDirectoryName);
}
