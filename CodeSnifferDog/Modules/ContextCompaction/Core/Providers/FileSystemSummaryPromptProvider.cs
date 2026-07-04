namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

/// <summary>
/// Loads the compaction summary prompt from a file on disk.
/// </summary>
/// <param name="promptFilePath">Absolute or relative path to the prompt file.</param>
public sealed class FileSystemSummaryPromptProvider(string promptFilePath) : ISummaryPromptProvider
{
    /// <inheritdoc />
    /// <exception cref="FileNotFoundException">The configured prompt file does not exist.</exception>
    public async ValueTask<string> GetPromptAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(promptFilePath))
            throw new FileNotFoundException("Operational context compaction prompt file was not found.", promptFilePath);

        return await File.ReadAllTextAsync(promptFilePath, cancellationToken).ConfigureAwait(false);
    }
}
