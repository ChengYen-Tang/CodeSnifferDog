namespace CodeSnifferDog.Modules.Prompts;

/// <summary>
/// Replaces named placeholder tokens inside prompt templates.
/// </summary>
public sealed class PromptTemplateRenderer
{
    private readonly StringComparison _comparison = StringComparison.Ordinal;

    /// <summary>
    /// Renders a prompt template with the supplied placeholder values.
    /// </summary>
    /// <param name="template">Prompt template text.</param>
    /// <param name="placeholders">Placeholder values keyed by token name.</param>
    /// <returns>The rendered prompt text.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="template"/> is blank or a placeholder key is blank.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="placeholders"/> or one of its values is <see langword="null"/>.</exception>
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
