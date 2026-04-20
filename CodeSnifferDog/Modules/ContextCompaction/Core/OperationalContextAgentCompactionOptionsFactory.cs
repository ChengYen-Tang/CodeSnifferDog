using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class OperationalContextAgentCompactionOptionsFactory(
    PromptAssetReader promptAssetReader,
    IOperationalContextCompactionSummarizer summarizer,
    IOperationalContextReactiveCompactionExceptionDecider? reactiveExceptionDecider = null)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;
    private readonly IOperationalContextCompactionSummarizer _summarizer = summarizer;
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

        OperationalContextChatReducer reducer = new(
            options,
            new FileSystemOperationalContextSummaryPromptProvider(promptPath),
            _summarizer,
            artifactsProvider: new MetadataOperationalContextCompactionArtifactsProvider(options),
            hooks,
            cleanupHandlers);

        return new OperationalContextAgentCompactionOptions
        {
            Reducer = reducer,
            CollapseController = new OperationalContextCollapseController(reducer),
            MessageShrinker = new OperationalContextMessageShrinker(),
            EnableReactiveCompactionRetry = enableReactiveCompactionRetry,
            ReactiveExceptionDecider = _reactiveExceptionDecider,
        };
    }
}
