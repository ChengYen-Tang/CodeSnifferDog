namespace CodeSnifferDog.Modules.Prompts;

public sealed class PromptAssetReader
{
    private const string PromptAssetRootDirectoryName = "prompts";
    private readonly string _baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);

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

    public string ReadRequiredPrompt(string relativePromptPath) =>
        File.ReadAllText(GetRequiredPromptPath(relativePromptPath));
}
