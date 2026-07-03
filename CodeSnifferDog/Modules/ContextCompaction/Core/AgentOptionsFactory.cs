using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

public sealed class AgentOptionsFactory(
    PromptAssetReader promptAssetReader,
    ISummarizer summarizer,
    IReactiveExceptionDecider? reactiveExceptionDecider = null)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;
    private readonly ISummarizer _summarizer = summarizer;
    private readonly IReactiveExceptionDecider _reactiveExceptionDecider =
        reactiveExceptionDecider ?? new DefaultReactiveExceptionDecider();

    public AgentCompactionOptions CreateFromPromptAsset(
        string summaryPromptAssetPath,
        CompactionOptions options,
        bool enableReactiveCompactionRetry = true,
        IEnumerable<IHook>? hooks = null,
        IEnumerable<ICleanupHandler>? cleanupHandlers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summaryPromptAssetPath);
        ArgumentNullException.ThrowIfNull(options);

        string promptPath = _promptAssetReader.GetRequiredPromptPath(summaryPromptAssetPath);

        ChatReducer reducer = new(
            options,
            new FileSystemSummaryPromptProvider(promptPath),
            _summarizer,
            artifactsProvider: new MetadataCompactionArtifactsProvider(options),
            hooks,
            cleanupHandlers);

        return new AgentCompactionOptions
        {
            Reducer = reducer,
            CollapseController = new CollapseController(reducer),
            MessageShrinker = new MessageShrinker(),
            EnableReactiveCompactionRetry = enableReactiveCompactionRetry,
            ReactiveExceptionDecider = _reactiveExceptionDecider,
        };
    }
}
