namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

public interface ISummaryPromptProvider
{
    ValueTask<string> GetPromptAsync(CancellationToken cancellationToken);
}
