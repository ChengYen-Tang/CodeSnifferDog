namespace CodeSnifferDog.Modules.Prompts;

public sealed class PromptAssetReader
{
    private const string PromptAssetRootDirectoryName = "prompts";

    public string GetRequiredPromptPath(string relativePromptPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePromptPath);

        string promptPath = Path.Combine(
            Path.GetFullPath(AppContext.BaseDirectory),
            PromptAssetRootDirectoryName,
            relativePromptPath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(promptPath))
            throw new FileNotFoundException("Prompt asset was not found.", promptPath);

        return promptPath;
    }

    public string ReadRequiredPrompt(string relativePromptPath) =>
        File.ReadAllText(GetRequiredPromptPath(relativePromptPath));
}
