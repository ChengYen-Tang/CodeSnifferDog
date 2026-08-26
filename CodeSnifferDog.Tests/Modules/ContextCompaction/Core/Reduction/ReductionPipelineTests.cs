using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Core.Reduction;

[TestClass]
public sealed class ReductionPipelineTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task CompactAsync_AddsSummaryContractToPrompt()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        ReductionPipeline pipeline = CreatePipeline(summarizer);

        await pipeline.CompactAsync(
            [new ChatMessage(ChatRole.User, new string('x', 1_000))],
            CompactionReason.AutomaticThreshold,
            TestContext.CancellationToken);

        Assert.IsNotNull(summarizer.LastSummaryPrompt);
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("Return text only.", StringComparison.Ordinal));
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("<summary>...</summary>", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CompactAsync_RunsHooksAndCleanup_OnSuccessfulCompaction()
    {
        RecordingHook hook = new();
        RecordingCleanupHandler cleanupHandler = new();
        ReductionPipeline pipeline = CreatePipeline(
            new RecordingSummarizer("<summary>Current objective\nCompleted work\nNext steps</summary>"),
            hooks: [hook],
            cleanupHandlers: [cleanupHandler]);

        await pipeline.CompactAsync(
            [new ChatMessage(ChatRole.User, new string('x', 1_000))],
            CompactionReason.AutomaticThreshold,
            TestContext.CancellationToken);

        Assert.AreEqual(1, hook.BeforeCallCount);
        Assert.AreEqual(1, hook.AfterCallCount);
        Assert.AreEqual(1, cleanupHandler.CallCount);
    }

    [TestMethod]
    public async Task CompactAsync_Throws_WhenSummaryDoesNotContainSummaryTag()
    {
        ReductionPipeline pipeline = CreatePipeline(new RecordingSummarizer("Current objective\nCompleted work\nNext steps"));

        await Assert.ThrowsExactlyAsync<CompactionException>(
            () => pipeline.CompactAsync(
                [new ChatMessage(ChatRole.User, new string('x', 1_000))],
                CompactionReason.AutomaticThreshold,
                TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CompactAsync_DoesNotSummarize_WhenFrameworkAutomaticThresholdIsNotMet()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        CompactionOptions options = new()
        {
            ModelContextWindowTokens = 100_000,
            PreservedTailMinTokens = 1,
            PreservedTailMinMessages = 1,
            PreservedTailMaxTokens = 10_000,
        };
        ReductionPipeline pipeline = new(
            options,
            new StaticSummaryPromptProvider("summarize the current run"),
            summarizer,
            artifactsProvider: null,
            hooks: null,
            cleanupHandlers: null,
            planner: new FrameworkCompactionPlanner(options));

        CompactionResult result = await pipeline.CompactAsync(
            [
                new ChatMessage(ChatRole.User, "small request"),
                new ChatMessage(ChatRole.Assistant, "small response"),
            ],
            CompactionReason.AutomaticThreshold,
            TestContext.CancellationToken);

        Assert.IsFalse(result.WasCompacted);
        Assert.AreEqual(0, summarizer.CallCount);
    }

    [TestMethod]
    public async Task CompactAsync_UsesFrameworkPlanForSummaryArtifactsHooksAndAtomicTail()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        RecordingHook hook = new();
        ChatMessage attachment = new(ChatRole.User, "preserved attachment");
        RecordingArtifactsProvider artifactsProvider = new(attachment);
        CompactionOptions options = new()
        {
            ModelContextWindowTokens = 3,
            SummaryReservedOutputTokens = 1,
            AutoCompactBufferTokens = 1,
            PreservedTailMinTokens = 1,
            PreservedTailMinMessages = 2,
            PreservedTailMaxTokens = 10_000,
        };
        ReductionPipeline pipeline = new(
            options,
            new StaticSummaryPromptProvider("summarize the current run"),
            summarizer,
            artifactsProvider,
            hooks: [hook],
            cleanupHandlers: null,
            planner: new FrameworkCompactionPlanner(options));
        ChatMessage[] messages =
        [
            new(ChatRole.System, "system"),
            new(ChatRole.User, "older request"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "ToolA", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", "result")]),
            new(ChatRole.Assistant, "final response"),
        ];

        CompactionResult result = await pipeline.CompactAsync(
            messages,
            CompactionReason.AutomaticThreshold,
            TestContext.CancellationToken);

        Assert.IsTrue(result.WasCompacted);
        Assert.AreEqual(1, summarizer.CallCount);
        Assert.AreEqual(1, hook.BeforeCallCount);
        Assert.AreEqual(1, hook.AfterCallCount);
        Assert.HasCount(3, result.MessagesToKeep);
        Assert.AreSame(messages[2], result.MessagesToKeep[0]);
        Assert.AreSame(messages[3], result.MessagesToKeep[1]);
        Assert.AreSame(messages[4], result.MessagesToKeep[2]);
        Assert.AreSame(attachment, Assert.ContainsSingle(result.AttachmentMessages));
        Assert.AreEqual(
            CompactionArtifactMetadata.SummaryArtifactKind,
            result.SummaryMessage.AdditionalProperties![CompactionArtifactMetadata.ArtifactKindKey]);
    }

    private static ReductionPipeline CreatePipeline(
        ISummarizer summarizer,
        IEnumerable<IHook>? hooks = null,
        IEnumerable<ICleanupHandler>? cleanupHandlers = null) => new(
            new CompactionOptions
            {
                ModelContextWindowTokens = 3,
                SummaryReservedOutputTokens = 1,
                AutoCompactBufferTokens = 1,
                PreservedTailMinTokens = 1,
                PreservedTailMinMessages = 1,
                PreservedTailMaxTokens = 10_000,
            },
            new StaticSummaryPromptProvider("summarize the current run"),
            summarizer,
            artifactsProvider: null,
            hooks,
            cleanupHandlers);

    private sealed class RecordingSummarizer(string response) : ISummarizer
    {
        public int CallCount { get; private set; }

        public string? LastSummaryPrompt { get; private set; }

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            CompactionOptions options,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastSummaryPrompt = summaryPrompt;
            return ValueTask.FromResult(response);
        }
    }

    private sealed class RecordingHook : IHook
    {
        public int BeforeCallCount { get; private set; }

        public int AfterCallCount { get; private set; }

        public ValueTask OnBeforeCompactionAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            CompactionReason reason,
            CancellationToken cancellationToken)
        {
            BeforeCallCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnAfterCompactionAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            IReadOnlyList<ChatMessage> compactedMessages,
            CompactionReason reason,
            CancellationToken cancellationToken)
        {
            AfterCallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingCleanupHandler : ICleanupHandler
    {
        public int CallCount { get; private set; }

        public ValueTask CleanupAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            IReadOnlyList<ChatMessage> compactedMessages,
            CompactionReason reason,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingArtifactsProvider(ChatMessage attachment) : ICompactionArtifactsProvider
    {
        public ValueTask<CompactionArtifacts> GetArtifactsAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            IReadOnlyList<ChatMessage> messagesToKeep,
            string normalizedSummary,
            CompactionReason reason,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new CompactionArtifacts
            {
                AttachmentMessages = [attachment],
                HookResultMessages = [],
            });
    }
}
