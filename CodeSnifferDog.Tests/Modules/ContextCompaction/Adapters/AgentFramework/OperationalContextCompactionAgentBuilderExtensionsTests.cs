using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Adapters.AgentFramework;

[TestClass]
public sealed class OperationalContextCompactionAgentBuilderExtensionsTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task InvokeWithReactiveCompactionRetryAsync_RetriesWithCompactedMessages_WhenExceptionIsCompactable()
    {
        RecordingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextAgentCompactionOptions options = CreateOptions(
            CreateReducer(summarizer),
            new DefaultOperationalContextReactiveCompactionExceptionDecider());

        ChatMessage[] originalMessages =
        [
            new(ChatRole.System, "system"),
            new(ChatRole.User, "user-1"),
            new(ChatRole.Assistant, "assistant-1"),
            new(ChatRole.User, "user-2"),
        ];

        List<IReadOnlyList<ChatMessage>> invocations = [];

        await OperationalContextCompactionAgentBuilderExtensions.InvokeWithReactiveCompactionRetryAsync(
            originalMessages,
            options,
            (messages, cancellationToken) =>
            {
                invocations.Add(messages);

                if (invocations.Count == 1)
                    throw new OperationalContextModelInvocationException(
                        OperationalContextModelInvocationFailureKind.ContextWindowExceeded,
                        "context too large");

                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        Assert.HasCount(2, invocations);
        CollectionAssert.AreEqual(originalMessages, invocations[0].ToArray());
        Assert.AreEqual("Operational context boundary marker", invocations[1][1].Text);
        Assert.AreEqual("Operational summary checkpoint", invocations[1][2].Text?.Split(Environment.NewLine)[0]);
    }

    [TestMethod]
    public async Task InvokeWithReactiveCompactionRetryAsync_DoesNotRetry_WhenExceptionIsNotCompactable()
    {
        OperationalContextAgentCompactionOptions options = CreateOptions(
            CreateReducer(new RecordingSummarizer("<summary>Current objective\nCompleted work\nNext steps</summary>")),
            new DefaultOperationalContextReactiveCompactionExceptionDecider());

        ChatMessage[] originalMessages = [new(ChatRole.User, "user")];
        int callCount = 0;

        await Assert.ThrowsExactlyAsync<OperationalContextModelInvocationException>(
            () => OperationalContextCompactionAgentBuilderExtensions.InvokeWithReactiveCompactionRetryAsync(
                originalMessages,
                options,
                (messages, cancellationToken) =>
                {
                    callCount++;
                    throw new OperationalContextModelInvocationException(
                        OperationalContextModelInvocationFailureKind.Unknown,
                        "boom");
                },
                TestContext.CancellationToken));

        Assert.AreEqual(1, callCount);
    }

    [TestMethod]
    public void MessagesAreEquivalentForRetry_ReturnsFalse_WhenAdditionalPropertiesDiffer()
    {
        ChatMessage left = new(ChatRole.Assistant, "same")
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["artifact"] = "a",
            },
        };
        ChatMessage right = new(ChatRole.Assistant, "same")
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["artifact"] = "b",
            },
        };

        bool equivalent = OperationalContextCompactionAgentBuilderExtensions.MessagesAreEquivalentForRetry([left], [right]);

        Assert.IsFalse(equivalent);
    }

    [TestMethod]
    public void MessagesAreEquivalentForRetry_ReturnsTrue_WhenAdditionalPropertiesMatchInDifferentOrder()
    {
        ChatMessage left = new(ChatRole.Assistant, "same")
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["b"] = 2,
                ["a"] = "x",
            },
        };
        ChatMessage right = new(ChatRole.Assistant, "same")
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["a"] = "x",
                ["b"] = 2,
            },
        };

        bool equivalent = OperationalContextCompactionAgentBuilderExtensions.MessagesAreEquivalentForRetry([left], [right]);

        Assert.IsTrue(equivalent);
    }

    private static OperationalContextAgentCompactionOptions CreateOptions(
        OperationalContextChatReducer reducer,
        IOperationalContextReactiveCompactionExceptionDecider decider) => new()
        {
            Reducer = reducer,
            AutomaticCompactionTrigger = CompactionTriggers.TokensExceed(10),
            ReactiveExceptionDecider = decider,
        };

    private static OperationalContextChatReducer CreateReducer(IOperationalContextCompactionSummarizer summarizer) => new(
        new OperationalContextCompactionOptions
        {
            ContextTokenThreshold = 10,
        },
        new StaticOperationalContextSummaryPromptProvider("summarize"),
        summarizer,
        new FixedUsageProvider(usedTokens: 100));

    private sealed class FixedUsageProvider(long usedTokens) : IOperationalContextCompactionUsageProvider
    {
        public ValueTask<OperationalContextCompactionUsage?> GetUsageAsync(
            IReadOnlyList<ChatMessage> messages,
            CancellationToken cancellationToken) => ValueTask.FromResult<OperationalContextCompactionUsage?>(new OperationalContextCompactionUsage
            {
                UsedTokens = usedTokens,
            });
    }

    private sealed class RecordingSummarizer(string response) : IOperationalContextCompactionSummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken) => ValueTask.FromResult(response);
    }

}
