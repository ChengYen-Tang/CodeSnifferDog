namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

public sealed class FileSystemSummaryPromptProvider(string promptFilePath) : ISummaryPromptProvider
{
    public async ValueTask<string> GetPromptAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(promptFilePath))
            throw new FileNotFoundException("Operational context compaction prompt file was not found.", promptFilePath);

        return await File.ReadAllTextAsync(promptFilePath, cancellationToken).ConfigureAwait(false);
    }
}
