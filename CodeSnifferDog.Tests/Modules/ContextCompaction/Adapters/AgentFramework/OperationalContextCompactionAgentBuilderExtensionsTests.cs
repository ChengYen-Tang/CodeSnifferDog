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
        Assert.HasCount(5, invocations[1]);
        Assert.AreEqual("Operational compact boundary", invocations[1][1].Text);
        Assert.AreEqual("Operational summary checkpoint", invocations[1][2].Text?.Split(Environment.NewLine)[0]);
        Assert.AreEqual(
            OperationalContextCompactionArtifactMetadata.ContinuityArtifactKind,
            invocations[1][3].AdditionalProperties![OperationalContextCompactionArtifactMetadata.ArtifactKindKey]);
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
    public async Task InvokeWithReactiveCompactionRetryAsync_ThrowsCompactionException_WhenReactiveCompactionFails()
    {
        OperationalContextAgentCompactionOptions options = CreateOptions(
            CreateReducer(new ThrowingSummarizer()),
            new DefaultOperationalContextReactiveCompactionExceptionDecider());

        ChatMessage[] originalMessages = [new(ChatRole.User, "user")];

        await Assert.ThrowsExactlyAsync<OperationalContextCompactionException>(
            () => OperationalContextCompactionAgentBuilderExtensions.InvokeWithReactiveCompactionRetryAsync(
                originalMessages,
                options,
                (messages, cancellationToken) => throw new OperationalContextModelInvocationException(
                    OperationalContextModelInvocationFailureKind.ContextWindowExceeded,
                    "context too large"),
                TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task InvokeWithReactiveCompactionRetryAsync_SnipsOlderCompactableToolMessages_BeforeReactiveCompaction()
    {
        CapturingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextAgentCompactionOptions options = CreateOptions(
            CreateReducer(
                summarizer,
                new OperationalContextCompactionOptions
                {
                    ModelContextWindowTokens = 100,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                    SnipTriggerToolResultCount = 3,
                    SnipKeepRecentToolResultCount = 1,
                }),
            new DefaultOperationalContextReactiveCompactionExceptionDecider());

        ChatMessage[] originalMessages =
        [
            new(ChatRole.Assistant, [new FunctionCallContent("call-1", "RunShellCommand", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-1", "result-1")]),
            new(ChatRole.Assistant, [new FunctionCallContent("call-2", "RunShellCommand", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-2", "result-2")]),
            new(ChatRole.Assistant, [new FunctionCallContent("call-3", "RunShellCommand", new Dictionary<string, object?>())]),
            new(ChatRole.Tool, [new FunctionResultContent("call-3", "result-3")]),
        ];

        await OperationalContextCompactionAgentBuilderExtensions.InvokeWithReactiveCompactionRetryAsync(
            originalMessages,
            options,
            (messages, cancellationToken) =>
            {
                if (messages.SequenceEqual(originalMessages))
                    throw new OperationalContextModelInvocationException(
                        OperationalContextModelInvocationFailureKind.ContextWindowExceeded,
                        "context too large");

                return Task.CompletedTask;
            },
            TestContext.CancellationToken);

        string[] summarizedCallIds = [.. summarizer.LastMessages!
            .SelectMany(static message => message.Contents.OfType<FunctionCallContent>())
            .Select(static call => call.CallId)];

        CollectionAssert.AreEqual(new[] { "call-3" }, summarizedCallIds);
    }

    [TestMethod]
    public async Task PrepareReactiveRetryAsync_CommitsCollapseState_InContextCollapseMode()
    {
        CapturingSummarizer summarizer = new("<summary>Current objective\nCompleted work\nNext steps</summary>");
        OperationalContextAgentCompactionOptions options = CreateOptions(
            CreateReducer(
                summarizer,
                new OperationalContextCompactionOptions
                {
                    ModelContextWindowTokens = 100,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                    Mode = OperationalContextCompactionMode.ContextCollapse,
                }),
            new DefaultOperationalContextReactiveCompactionExceptionDecider());

        ChatMessage[] originalMessages =
        [
            new(ChatRole.User, "user-1"),
            new(ChatRole.Assistant, "assistant-1"),
            new(ChatRole.User, new string('x', 1_000)),
        ];
        TestSession session = new();

        _ = await options.CollapseController!
            .PrepareReactiveRetryAsync(originalMessages, session, TestContext.CancellationToken)
            .AsTask()
            .ConfigureAwait(false);

        OperationalContextCollapseState state = new OperationalContextCollapseSessionState().Get(session);
        Assert.AreEqual(OperationalContextCompactionReason.Reactive.ToString(), state.LastCollapseReason);
        Assert.HasCount(1, state.Commits);
        Assert.IsEmpty(state.StagedSpans);
        Assert.IsFalse(string.IsNullOrWhiteSpace(state.Commits[0].CollapseId));
        Assert.IsFalse(string.IsNullOrWhiteSpace(state.Commits[0].SummaryMessageId));
        Assert.IsFalse(string.IsNullOrWhiteSpace(state.Commits[0].ProjectionMessageId));
        Assert.IsFalse(string.IsNullOrWhiteSpace(state.Commits[0].ContinuityProjectionMessageId));
        Assert.IsFalse(string.IsNullOrWhiteSpace(state.Commits[0].Summary));
        Assert.AreEqual(ChatRole.User.ToString(), state.Commits[0].FirstArchivedMessageRole);
        Assert.AreEqual(ChatRole.Assistant.ToString(), state.Commits[0].LastArchivedMessageRole);
        Assert.AreEqual(state.Commits[0].CollapseId, state.Snapshot.LastCommittedCollapseId);
        Assert.IsNull(state.Snapshot.LastStagedCollapseId);
    }

    [TestMethod]
    public void CommitStagedCollapsesAndPrepareRetryMessages_UsesCommittedProjection_InContextCollapseMode()
    {
        OperationalContextAgentCompactionOptions options = CreateOptions(
            CreateReducer(
                new RecordingSummarizer("<summary>Current objective\nCompleted work\nNext steps</summary>"),
                new OperationalContextCompactionOptions
                {
                    ModelContextWindowTokens = 100,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                    Mode = OperationalContextCompactionMode.ContextCollapse,
                }),
            new DefaultOperationalContextReactiveCompactionExceptionDecider());
        TestSession session = new();

        ChatMessage[] originalMessages =
        [
            new(ChatRole.User, "user-1"),
            new(ChatRole.Assistant, "assistant-1"),
            new(ChatRole.User, new string('x', 1_000)),
        ];

        _ = options.CollapseController!.PrepareReactiveRetryAsync(
            originalMessages,
            session,
            TestContext.CancellationToken).AsTask().GetAwaiter().GetResult();

        object? result = typeof(OperationalContextCompactionAgentBuilderExtensions)
            .GetMethod("CommitStagedCollapsesAndPrepareRetryMessages", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, [originalMessages, session, options, new HashSet<string>(StringComparer.Ordinal) { "0000000000000001" }]);

        ChatMessage[] retryMessages = ((IReadOnlyList<ChatMessage>)result!).ToArray();

        Assert.IsTrue(retryMessages.Any(message =>
            message.AdditionalProperties?.GetValueOrDefault(OperationalContextCompactionArtifactMetadata.ArtifactKindKey)?.ToString() ==
            OperationalContextCompactionArtifactMetadata.CollapseProjectionArtifactKind));
        Assert.IsFalse(retryMessages.SequenceEqual(originalMessages));
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

    [TestMethod]
    public void MessagesAreEquivalentForRetry_ReturnsFalse_WhenContentsDiffer()
    {
        ChatMessage left = new(ChatRole.Assistant, [new FunctionCallContent("call-1", "ToolA", new Dictionary<string, object?> { ["value"] = 1 })]);
        ChatMessage right = new(ChatRole.Assistant, [new FunctionCallContent("call-1", "ToolA", new Dictionary<string, object?> { ["value"] = 2 })]);

        bool equivalent = OperationalContextCompactionAgentBuilderExtensions.MessagesAreEquivalentForRetry([left], [right]);

        Assert.IsFalse(equivalent);
    }

    private static OperationalContextAgentCompactionOptions CreateOptions(
        OperationalContextChatReducer reducer,
        IOperationalContextReactiveCompactionExceptionDecider decider) => new()
        {
            Reducer = reducer,
            CollapseController = new OperationalContextCollapseController(reducer),
            MessageShrinker = new OperationalContextMessageShrinker(),
            ReactiveExceptionDecider = decider,
        };

    private static OperationalContextChatReducer CreateReducer(
        IOperationalContextCompactionSummarizer summarizer,
        OperationalContextCompactionOptions? options = null) => new(
        options ?? new OperationalContextCompactionOptions
        {
            ModelContextWindowTokens = 100,
            SummaryReservedOutputTokens = 1,
            AutoCompactBufferTokens = 1,
            PreservedTailMinTokens = 1,
            PreservedTailMinMessages = 1,
        },
        new StaticOperationalContextSummaryPromptProvider("summarize"),
        summarizer);

    private sealed class RecordingSummarizer(string response) : IOperationalContextCompactionSummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken) => ValueTask.FromResult(response);
    }

    private sealed class CapturingSummarizer(string response) : IOperationalContextCompactionSummarizer
    {
        public IReadOnlyList<ChatMessage>? LastMessages { get; private set; }

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken)
        {
            LastMessages = messages;
            return ValueTask.FromResult(response);
        }
    }

    private sealed class ThrowingSummarizer : IOperationalContextCompactionSummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            OperationalContextCompactionOptions options,
            CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }

    private sealed class TestSession : Microsoft.Agents.AI.AgentSession;
}
