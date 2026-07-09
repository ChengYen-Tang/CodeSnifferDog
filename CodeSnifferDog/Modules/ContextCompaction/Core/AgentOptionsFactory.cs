using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using Microsoft.Extensions.Logging;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

/// <summary>
/// Creates agent-ready compaction options from a prompt asset and shared summarization dependencies.
/// </summary>
/// <param name="promptAssetReader">Prompt asset resolver used to locate the summary prompt file.</param>
/// <param name="summarizer">Summarizer implementation that will generate compaction summaries.</param>
/// <param name="reactiveExceptionDecider">Optional policy that decides whether reactive retries should occur after failures.</param>
public sealed class AgentOptionsFactory(
    PromptAssetReader promptAssetReader,
    ISummarizer summarizer,
    IReactiveExceptionDecider? reactiveExceptionDecider = null)
{
    private readonly PromptAssetReader _promptAssetReader = promptAssetReader;
    private readonly ISummarizer _summarizer = summarizer;
    private readonly IReactiveExceptionDecider _reactiveExceptionDecider =
        reactiveExceptionDecider ?? new DefaultReactiveExceptionDecider();

    /// <summary>
    /// Creates a complete <see cref="AgentCompactionOptions" /> instance backed by a prompt asset on disk.
    /// </summary>
    /// <param name="summaryPromptAssetPath">Prompt asset path used to resolve the summary instructions file.</param>
    /// <param name="options">Compaction settings that configure reducer thresholds and shrinking behavior.</param>
    /// <param name="enableReactiveCompactionRetry"><see langword="true" /> to enable reactive retry flows after compaction-related failures.</param>
    /// <param name="hooks">Optional compaction hooks that observe before and after transcript rewriting.</param>
    /// <param name="cleanupHandlers">Optional cleanup handlers that run after successful compaction.</param>
    /// <param name="loggerFactory">Optional logger factory used by agent-framework compaction adapters.</param>
    /// <returns>The agent-facing compaction options wired to the resolved prompt asset.</returns>
    /// <exception cref="ArgumentException"><paramref name="summaryPromptAssetPath" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    public AgentCompactionOptions CreateFromPromptAsset(
        string summaryPromptAssetPath,
        CompactionOptions options,
        bool enableReactiveCompactionRetry = true,
        IEnumerable<IHook>? hooks = null,
        IEnumerable<ICleanupHandler>? cleanupHandlers = null,
        ILoggerFactory? loggerFactory = null)
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
            LoggerFactory = loggerFactory,
        };
    }
}
