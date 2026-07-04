namespace CodeSnifferDog.Modules.Prompts;

/// <summary>
/// Resolves and reads prompt assets from the application's prompt directory.
/// </summary>
public sealed class PromptAssetReader
{
    private const string PromptAssetRootDirectoryName = "prompts";
    private readonly string _baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);

    /// <summary>
    /// Gets the full path of a required prompt asset.
    /// </summary>
    /// <param name="relativePromptPath">Prompt path relative to the prompt root.</param>
    /// <returns>The full prompt asset path.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="relativePromptPath"/> is blank.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the prompt asset does not exist.</exception>
    public string GetRequiredPromptPath(string relativePromptPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePromptPath);

        string promptPath = Path.Combine(
            _baseDirectory,
            PromptAssetRootDirectoryName,
            relativePromptPath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(promptPath))
            throw new FileNotFoundException("Prompt asset was not found.", promptPath);

        return promptPath;
    }

    /// <summary>
    /// Reads one required prompt asset.
    /// </summary>
    /// <param name="relativePromptPath">Prompt path relative to the prompt root.</param>
    /// <returns>The prompt text.</returns>
    public string ReadRequiredPrompt(string relativePromptPath) =>
        File.ReadAllText(GetRequiredPromptPath(relativePromptPath));
}
