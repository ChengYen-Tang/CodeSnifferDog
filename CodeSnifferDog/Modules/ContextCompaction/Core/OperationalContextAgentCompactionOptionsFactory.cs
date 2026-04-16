using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using Microsoft.Agents.AI.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class OperationalContextAgentCompactionOptionsFactory(
    PromptAssetReader promptAssetReader,
    IOperationalContextCompactionSummarizer summarizer,
    IOperationalContextCompactionUsageProvider usageProvider,
    IOperationalContextReactiveCompactionExceptionDecider? reactiveExceptionDecider = null)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;
    private readonly IOperationalContextCompactionSummarizer _summarizer = summarizer;
    private readonly IOperationalContextCompactionUsageProvider _usageProvider = usageProvider;
    private readonly IOperationalContextReactiveCompactionExceptionDecider _reactiveExceptionDecider =
        reactiveExceptionDecider ?? new DefaultOperationalContextReactiveCompactionExceptionDecider();

    public OperationalContextAgentCompactionOptions CreateFromPromptAsset(
        string summaryPromptAssetPath,
        OperationalContextCompactionOptions options,
        bool enableReactiveCompactionRetry = true,
        IEnumerable<IOperationalContextCompactionHook>? hooks = null,
        IEnumerable<IOperationalContextCompactionCleanupHandler>? cleanupHandlers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summaryPromptAssetPath);
        ArgumentNullException.ThrowIfNull(options);

        string promptPath = _promptAssetReader.GetRequiredPromptPath(summaryPromptAssetPath);

        return new OperationalContextAgentCompactionOptions
        {
            Reducer = new OperationalContextChatReducer(
                options,
                new FileSystemOperationalContextSummaryPromptProvider(promptPath),
                _summarizer,
                _usageProvider,
                hooks,
                cleanupHandlers),
            AutomaticCompactionTrigger = CompactionTriggers.TokensExceed(options.ContextTokenThreshold),
            EnableReactiveCompactionRetry = enableReactiveCompactionRetry,
            ReactiveExceptionDecider = _reactiveExceptionDecider,
        };
    }
}
