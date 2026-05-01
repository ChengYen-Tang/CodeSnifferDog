namespace CodeSnifferDog.Modules.Prompts;

public sealed class PromptTemplateRenderer
{
    private readonly StringComparison _comparison = StringComparison.Ordinal;

    public string Render(
        string template,
        IReadOnlyDictionary<string, string> placeholders)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(placeholders);

        string rendered = template;

        foreach ((string key, string value) in placeholders)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(value);
            rendered = rendered.Replace($"{{{{{key}}}}}", value, _comparison);
        }

        return rendered;
    }
}
