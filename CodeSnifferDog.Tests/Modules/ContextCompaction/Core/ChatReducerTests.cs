using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Core;

[TestClass]
public sealed class ChatReducerTests
{
    private const string LongUserText = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task ReduceAsync_DoesNotCompact_WhenUsageIsBelowThreshold()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        ChatReducer reducer = CreateReducer(
            summarizer,
            modelContextWindowTokens: 50_000);

        ChatMessage[] messages =
        [
            new(ChatRole.System, "system"),
            new(ChatRole.User, "user"),
        ];

        ChatMessage[] reduced = [.. await reducer.ReduceAsync(messages, TestContext.CancellationToken)];

        Assert.HasCount(2, reduced);
        Assert.AreEqual(0, summarizer.CallCount);
        CollectionAssert.AreEqual(messages, reduced);
    }

    [TestMethod]
    public async Task ReduceAsync_ReplacesHistory_WithSingleSummaryCheckpoint_WhenThresholdIsExceeded()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        ChatReducer reducer = CreateReducer(
            summarizer,
            modelContextWindowTokens: 3,
            summaryReservedOutputTokens: 1,
            autoCompactBufferTokens: 1,
            preservedTailMinMessages: 1,
            preservedTailMinTokens: 1,
            preservedTailMaxTokens: 10_000);

        ChatMessage[] reduced =
        [
            .. await reducer.ReduceAsync(
                [
                    new ChatMessage(ChatRole.System, "system-1"),
                    new ChatMessage(ChatRole.User, LongUserText),
                    new ChatMessage(ChatRole.Assistant, LongUserText),
                    new ChatMessage(ChatRole.User, LongUserText),
                ],
                TestContext.CancellationToken),
        ];

        Assert.HasCount(5, reduced);
        Assert.AreEqual(ChatRole.System, reduced[0].Role);
        Assert.AreEqual(ChatRole.Assistant, reduced[2].Role);
        Assert.IsTrue(reduced[2].Text?.StartsWith("Operational summary checkpoint", StringComparison.Ordinal) ?? false);
        Assert.AreEqual(
            CompactionArtifactMetadata.SummaryArtifactKind,
            reduced[2].AdditionalProperties![CompactionArtifactMetadata.ArtifactKindKey]);
        Assert.AreEqual(
            CompactionArtifactMetadata.ContinuityArtifactKind,
            reduced[3].AdditionalProperties![CompactionArtifactMetadata.ArtifactKindKey]);
        bool isCompactionSummary = reduced[2].AdditionalProperties![CompactionArtifactMetadata.IsCompactionSummaryKey] is true;
        Assert.IsTrue(isCompactionSummary);
        Assert.AreEqual(1, summarizer.CallCount);
    }

    [TestMethod]
    public async Task ReduceAsync_Throws_WhenSummaryIsMissingRequiredFragments()
    {
        ChatReducer reducer = CreateReducer(
            new RecordingSummarizer("<summary>only partial summary</summary>"),
            modelContextWindowTokens: 3,
            summaryReservedOutputTokens: 1,
            autoCompactBufferTokens: 1);

        await Assert.ThrowsExactlyAsync<CompactionException>(
            () => reducer.ReduceAsync([new ChatMessage(ChatRole.User, LongUserText)], TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReduceAsync_Throws_WhenSummaryDoesNotContainSummaryTag()
    {
        ChatReducer reducer = CreateReducer(
            new RecordingSummarizer("Current objective\nCompleted work\nNext steps"),
            modelContextWindowTokens: 3,
            summaryReservedOutputTokens: 1,
            autoCompactBufferTokens: 1);

        await Assert.ThrowsExactlyAsync<CompactionException>(
            () => reducer.ReduceAsync([new ChatMessage(ChatRole.User, LongUserText)], TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReduceAsync_AddsFixedSummaryContractToPrompt()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        ChatReducer reducer = CreateReducer(
            summarizer,
            modelContextWindowTokens: 3,
            summaryReservedOutputTokens: 1,
            autoCompactBufferTokens: 1);

        await reducer.ReduceAsync([new ChatMessage(ChatRole.User, LongUserText)], TestContext.CancellationToken);

        Assert.IsNotNull(summarizer.LastSummaryPrompt);
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("Return text only.", StringComparison.Ordinal));
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("Do not call tools.", StringComparison.Ordinal));
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("<summary>...</summary>", StringComparison.Ordinal));
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("Use the following section headings exactly as written:", StringComparison.Ordinal));
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("  - Current objective", StringComparison.Ordinal));
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("  - Completed work", StringComparison.Ordinal));
        Assert.IsTrue(summarizer.LastSummaryPrompt.Contains("  - Next steps", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SummaryPromptAssets_UseTheValidatedSectionHeadings()
    {
        string promptDirectory = Path.Combine(AppContext.BaseDirectory, "prompts", "compaction");
        string[] promptFiles =
        [
            "scan-summary.md",
            "project-plan-summary.md",
            "rule-review-summary.md",
            "report-summary.md",
        ];

        foreach (string promptFile in promptFiles)
        {
            string prompt = File.ReadAllText(Path.Combine(promptDirectory, promptFile));

            StringAssert.Contains(prompt, "1. Current objective");
            StringAssert.Contains(prompt, "2. Completed work");
            StringAssert.Contains(prompt, "5. Next steps");
            Assert.IsFalse(prompt.Contains("Work completed", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task ReduceAsync_Throws_WhenSummarizerFails()
    {
        ChatReducer reducer = CreateReducer(
            new ThrowingSummarizer(),
            modelContextWindowTokens: 3,
            summaryReservedOutputTokens: 1,
            autoCompactBufferTokens: 1);

        await Assert.ThrowsExactlyAsync<CompactionException>(
            () => reducer.ReduceAsync([new ChatMessage(ChatRole.User, LongUserText)], TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReduceReactiveAsync_Throws_WhenSummarizerFails()
    {
        ChatReducer reducer = CreateReducer(
            new ThrowingSummarizer(),
            modelContextWindowTokens: 20_000);

        await Assert.ThrowsExactlyAsync<CompactionException>(
            () => reducer.ReduceReactiveAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReduceReactiveAsync_BypassesAutomaticThreshold()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        ChatReducer reducer = CreateReducer(
            summarizer,
            modelContextWindowTokens: 20_000);

        ChatMessage[] reduced = [.. await reducer.ReduceReactiveAsync([new ChatMessage(ChatRole.User, "user")], TestContext.CancellationToken)];

        Assert.AreEqual(1, summarizer.CallCount);
        Assert.HasCount(4, reduced);
    }

    [TestMethod]
    public async Task CompactReactiveAsync_TracksArchivedMessageReferences()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        ChatReducer reducer = CreateReducer(
            summarizer,
            modelContextWindowTokens: 20_000,
            preservedTailMinMessages: 1,
            preservedTailMinTokens: 1,
            preservedTailMaxTokens: 10_000);

        CompactionResult result = await reducer.CompactReactiveAsync(
            [
                new ChatMessage(ChatRole.User, "user-1"),
                new ChatMessage(ChatRole.Assistant, "assistant-1"),
                new ChatMessage(ChatRole.User, "user-2"),
            ],
            TestContext.CancellationToken);

        Assert.IsTrue(result.WasCompacted);
        Assert.HasCount(2, result.ArchivedMessageReferences);
        Assert.AreEqual(0, result.ArchivedMessageReferences[0].MessageIndex);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ArchivedMessageReferences[0].MessageId));
        Assert.AreEqual(1, result.ArchivedMessageReferences[1].MessageIndex);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ArchivedMessageReferences[1].MessageId));
        Assert.AreEqual(
            CompactionArtifactMetadata.ContinuityArtifactKind,
            result.ContinuityStateMessage.AdditionalProperties![CompactionArtifactMetadata.ArtifactKindKey]);
    }

    [TestMethod]
    public async Task ReduceAsync_UsesPreCallEstimatedTokens_ForAutomaticThreshold()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        ChatReducer reducer = CreateReducer(
            summarizer,
            modelContextWindowTokens: 100,
            summaryReservedOutputTokens: 1,
            autoCompactBufferTokens: 1);

        await reducer.ReduceAsync([new ChatMessage(ChatRole.User, new string('x', 1_000))], TestContext.CancellationToken);

        Assert.AreEqual(1, summarizer.CallCount);
    }

    [TestMethod]
    public async Task ReduceAsync_RunsHooksAndCleanup_OnSuccessfulCompaction()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        RecordingHook hook = new();
        RecordingCleanupHandler cleanupHandler = new();
        ChatReducer reducer = CreateReducer(
            summarizer,
            modelContextWindowTokens: 3,
            summaryReservedOutputTokens: 1,
            autoCompactBufferTokens: 1,
            hooks: [hook],
            cleanupHandlers: [cleanupHandler]);

        await reducer.ReduceAsync([new ChatMessage(ChatRole.User, LongUserText)], TestContext.CancellationToken);

        Assert.AreEqual(1, hook.BeforeCallCount);
        Assert.AreEqual(1, hook.AfterCallCount);
        Assert.AreEqual(1, cleanupHandler.CallCount);
        Assert.AreEqual(CompactionReason.AutomaticThreshold, hook.LastReason);
        Assert.AreEqual(CompactionReason.AutomaticThreshold, cleanupHandler.LastReason);
    }

    [TestMethod]
    public async Task ReduceAsync_ReinjectsAttachmentAndHookArtifacts_OutsidePreservedTail()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        ChatReducer reducer = CreateReducer(
            summarizer,
            modelContextWindowTokens: 3,
            summaryReservedOutputTokens: 1,
            autoCompactBufferTokens: 1,
            preservedTailMinMessages: 1,
            preservedTailMinTokens: 1,
            preservedTailMaxTokens: 10_000,
            artifactsProvider: new MetadataCompactionArtifactsProvider(
                new CompactionOptions
                {
                    ModelContextWindowTokens = 3,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                    PreservedTailMaxTokens = 10_000,
                    PostCompactAttachmentTokenBudget = 10_000,
                }));

        ChatMessage attachment = CreateArtifactMessage(
            ChatRole.User,
            "attachment",
            CompactionArtifactMetadata.AttachmentArtifactKind);
        ChatMessage hookResult = CreateArtifactMessage(
            ChatRole.User,
            "hook-result",
            CompactionArtifactMetadata.HookResultArtifactKind);

        ChatMessage[] reduced =
        [
            .. await reducer.ReduceAsync(
                [
                    new ChatMessage(ChatRole.System, "system-1"),
                    attachment,
                    hookResult,
                    new ChatMessage(ChatRole.User, LongUserText),
                ],
                TestContext.CancellationToken),
        ];

        CollectionAssert.Contains(reduced, attachment);
        CollectionAssert.Contains(reduced, hookResult);
    }

    private static ChatReducer CreateReducer(
        ISummarizer summarizer,
        long modelContextWindowTokens,
        int? summaryReservedOutputTokens = null,
        int? autoCompactBufferTokens = null,
        int? preservedTailMinTokens = null,
        int? preservedTailMinMessages = null,
        int? preservedTailMaxTokens = null,
        ICompactionArtifactsProvider? artifactsProvider = null,
        IEnumerable<IHook>? hooks = null,
        IEnumerable<ICleanupHandler>? cleanupHandlers = null) => new(
            new CompactionOptions
            {
                ModelContextWindowTokens = modelContextWindowTokens,
                SummaryReservedOutputTokens = summaryReservedOutputTokens ?? 1,
                AutoCompactBufferTokens = autoCompactBufferTokens ?? 1,
                PreservedTailMinTokens = preservedTailMinTokens ?? 1,
                PreservedTailMinMessages = preservedTailMinMessages ?? 1,
                PreservedTailMaxTokens = preservedTailMaxTokens ?? 10_000,
            },
            new StaticSummaryPromptProvider("summarize the current run"),
            summarizer,
            artifactsProvider,
            hooks: hooks,
            cleanupHandlers: cleanupHandlers);

    private static ChatMessage CreateArtifactMessage(ChatRole role, string text, string artifactKind)
    {
        ChatMessage message = new(role, text)
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [CompactionArtifactMetadata.ArtifactKindKey] = artifactKind,
            },
        };

        return message;
    }

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

    private sealed class ThrowingSummarizer : ISummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            CompactionOptions options,
            CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }

    private sealed class RecordingHook : IHook
    {
        public int BeforeCallCount { get; private set; }

        public int AfterCallCount { get; private set; }

        public CompactionReason? LastReason { get; private set; }

        public ValueTask OnBeforeCompactionAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            CompactionReason reason,
            CancellationToken cancellationToken)
        {
            BeforeCallCount++;
            LastReason = reason;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnAfterCompactionAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            IReadOnlyList<ChatMessage> compactedMessages,
            CompactionReason reason,
            CancellationToken cancellationToken)
        {
            AfterCallCount++;
            LastReason = reason;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingCleanupHandler : ICleanupHandler
    {
        public int CallCount { get; private set; }

        public CompactionReason? LastReason { get; private set; }

        public ValueTask CleanupAsync(
            IReadOnlyList<ChatMessage> originalMessages,
            IReadOnlyList<ChatMessage> compactedMessages,
            CompactionReason reason,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastReason = reason;
            return ValueTask.CompletedTask;
        }
    }
}
