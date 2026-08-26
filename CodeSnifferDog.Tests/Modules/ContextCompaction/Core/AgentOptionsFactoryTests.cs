using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Modules.Prompts;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Core;

[TestClass]
public sealed class AgentOptionsFactoryTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task CreateFromPromptAsset_UsesFrameworkPlanningWithTailArtifactsAndHooks()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        RecordingHook hook = new();
        CompactionOptions options = new()
        {
            ModelContextWindowTokens = 3,
            SummaryReservedOutputTokens = 1,
            AutoCompactBufferTokens = 1,
            PreservedTailMinTokens = 1,
            PreservedTailMinMessages = 2,
            PreservedTailMaxTokens = 10_000,
            PostCompactAttachmentTokenBudget = 10_000,
        };
        ChatReducer reducer = new AgentOptionsFactory(new PromptAssetReader(), summarizer)
            .CreateFromPromptAsset(
                ProjectPlanAgentPromptAssets.ProjectPlanSummaryPrompt,
                options,
                hooks: [hook])
            .Reducer;
        ChatMessage attachment = new(ChatRole.User, "preserved attachment")
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [CompactionArtifactMetadata.ArtifactKindKey] = CompactionArtifactMetadata.AttachmentArtifactKind,
            },
        };
        ChatMessage[] messages =
        [
            new(ChatRole.System, "system"),
            attachment,
            new(ChatRole.User, "older request"),
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "ToolA", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", "result")]),
            new(ChatRole.Assistant, "final response"),
        ];

        CompactionResult result = await reducer.CompactAutomaticAsync(messages, TestContext.CancellationToken);

        Assert.IsTrue(result.WasCompacted);
        Assert.AreEqual(1, summarizer.CallCount);
        Assert.AreEqual(1, hook.BeforeCallCount);
        Assert.AreEqual(1, hook.AfterCallCount);
        Assert.HasCount(3, result.MessagesToKeep);
        Assert.AreSame(messages[3], result.MessagesToKeep[0]);
        Assert.AreSame(messages[4], result.MessagesToKeep[1]);
        Assert.AreSame(messages[5], result.MessagesToKeep[2]);
        Assert.AreSame(attachment, Assert.ContainsSingle(result.AttachmentMessages));
    }

    private sealed class RecordingSummarizer(string response) : ISummarizer
    {
        public int CallCount { get; private set; }

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            CompactionOptions options,
            CancellationToken cancellationToken)
        {
            CallCount++;
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
}
