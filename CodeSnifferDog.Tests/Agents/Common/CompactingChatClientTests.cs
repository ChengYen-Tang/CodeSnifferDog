using CodeSnifferDog.Agents.Common;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Failures;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Modules.ContextCompaction.Core.Providers;
using CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;
using CodeSnifferDog.Workflows.Common;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Agents.Common;

[TestClass]
public sealed class CompactingChatClientTests
{
    [TestMethod]
    public async Task FunctionLoop_CompactsEveryProviderRequest()
    {
        RecordingChatClient provider = new();
        RecordingSummarizer summarizer = new();
        AgentCompactionOptions options = new()
        {
            Reducer = new ChatReducer(
                new CompactionOptions
                {
                    ModelContextWindowTokens = 100,
                    SummaryReservedOutputTokens = 1,
                    AutoCompactBufferTokens = 1,
                    PreservedTailMinTokens = 1,
                    PreservedTailMinMessages = 1,
                    PreservedTailMaxTokens = 10_000,
                },
                new StaticSummaryPromptProvider("Summarize."),
                summarizer),
        };
        IChatClient client = new FunctionInvokingChatClient(new CompactingChatClient(provider, options));
        AITool tool = AIFunctionFactory.Create(() => "result", "TestTool", "Test tool.", serializerOptions: null);
        ChatMessage[] history = Enumerable.Range(0, 10)
            .Select(index => new ChatMessage(ChatRole.User, $"history {index}: {new string('x', 10_000)}"))
            .ToArray();

        ChatResponse response = await AgentRunAttemptContext.RunAsync(
            Guid.CreateVersion7(),
            () => client.GetResponseAsync(history, new ChatOptions { Tools = [tool] }));

        Assert.AreEqual("done", response.Text);
        Assert.HasCount(2, provider.Requests);
        Assert.IsTrue(provider.Requests.All(ContainsSummaryCheckpoint));
        Assert.IsTrue(provider.Requests.All(request => request.Count(IsSummaryCheckpoint) == 1));
        Assert.IsFalse(summarizer.Inputs.Skip(1).Any(input => input.Any(message =>
            message.Text?.StartsWith("history 0:", StringComparison.Ordinal) == true)));
    }

    private static bool ContainsSummaryCheckpoint(IReadOnlyList<ChatMessage> messages) =>
        messages.Any(IsSummaryCheckpoint);

    private static bool IsSummaryCheckpoint(ChatMessage message) =>
        message.AdditionalProperties?.ContainsKey(CompactionArtifactMetadata.IsCompactionSummaryKey) == true;

    [TestMethod]
    public async Task AutomaticCompactionFailure_WithContextWindowError_DoesNotSendUncompactedContext()
    {
        RecordingChatClient provider = new();
        AgentCompactionOptions options = new()
        {
            Reducer = new ChatReducer(CreateOptions(), new StaticSummaryPromptProvider("Summarize."), new ContextWindowThrowingSummarizer()),
        };
        CompactingChatClient client = new(provider, options);
        ChatMessage[] messages =
        [
            new(ChatRole.User, new string('x', 10_000)),
            new(ChatRole.Assistant, "recent tail"),
        ];

        ModelInvocationException exception = await Assert.ThrowsExactlyAsync<ModelInvocationException>(
            () => client.GetResponseAsync(messages));

        Assert.AreEqual(ModelInvocationFailureKind.ContextWindowExceeded, exception.FailureKind);
        Assert.IsEmpty(provider.Requests);
    }

    [TestMethod]
    public async Task CallsOutsideAnAttempt_DoNotShareAutomaticFailureState()
    {
        ToggleSummarizer summarizer = new() { ShouldThrow = true };
        CompactingChatClient client = new(new RecordingChatClient(), new AgentCompactionOptions
        {
            Reducer = new ChatReducer(CreateOptions(), new StaticSummaryPromptProvider("Summarize."), summarizer),
        });
        ChatMessage[] messages = [new(ChatRole.User, new string('x', 10_000))];

        for (int index = 0; index < 3; index++)
            _ = await client.GetResponseAsync(messages);
        summarizer.ShouldThrow = false;
        _ = await client.GetResponseAsync(messages);

        Assert.AreEqual(4, summarizer.CallCount);
    }

    [TestMethod]
    public async Task ProviderUsageBias_TriggersCompactionOnTheNextCallWithinTheSameAttempt()
    {
        ChatMessage[] messages = [new(ChatRole.User, new string('x', 300))];
        int rawEstimateTokens = TokenEstimator.Estimate(messages);
        UsageReportingChatClient provider = new(inputTokenCount: rawEstimateTokens + 600);
        RecordingSummarizer summarizer = new();
        CompactingChatClient client = new(provider, new AgentCompactionOptions
        {
            Reducer = new ChatReducer(
                CreateOptions(modelContextWindowTokens: rawEstimateTokens + 3),
                new StaticSummaryPromptProvider("Summarize."),
                summarizer),
        });

        _ = await AgentRunAttemptContext.RunAsync(
            Guid.CreateVersion7(),
            async () =>
            {
                _ = await client.GetResponseAsync(messages);
                _ = await client.GetResponseAsync(messages);
                return true;
            });

        Assert.HasCount(2, provider.Requests);
        Assert.HasCount(1, summarizer.Inputs);
        Assert.IsTrue(ContainsSummaryCheckpoint(provider.Requests[1]));
    }

    [TestMethod]
    public async Task ProviderUsageCheckpoint_CrossesAgentAttemptsWithinOneLogicalRun()
    {
        ChatMessage[] messages = [new(ChatRole.User, new string('x', 300))];
        int rawEstimateTokens = TokenEstimator.Estimate(messages);
        UsageReportingChatClient provider = new(inputTokenCount: rawEstimateTokens + 600);
        RecordingSummarizer summarizer = new();
        CompactingChatClient client = new(provider, new AgentCompactionOptions
        {
            Reducer = new ChatReducer(
                CreateOptions(modelContextWindowTokens: rawEstimateTokens + 3),
                new StaticSummaryPromptProvider("Summarize."),
                summarizer),
        });

        _ = await AgentRunAttemptContext.RunLogicalRunAsync(
            async () =>
            {
                _ = await AgentRunAttemptContext.RunAsync(
                    Guid.CreateVersion7(),
                    async () =>
                    {
                        _ = await client.GetResponseAsync(messages);
                        return true;
                    });
                _ = await AgentRunAttemptContext.RunAsync(
                    Guid.CreateVersion7(),
                    async () =>
                    {
                        _ = await client.GetResponseAsync(messages);
                        return true;
                    });
                return true;
            });

        Assert.HasCount(2, provider.Requests);
        Assert.HasCount(1, summarizer.Inputs);
        Assert.IsTrue(ContainsSummaryCheckpoint(provider.Requests[1]));
    }

    [TestMethod]
    public async Task ProviderUsageBelowLocalEstimate_DoesNotTriggerCompactionForTheAppendedContext()
    {
        ChatMessage firstMessage = new(ChatRole.User, new string('x', 1_200));
        ChatMessage appendedMessage = new(ChatRole.User, new string('y', 200));
        ChatMessage[] firstRequest = [firstMessage];
        ChatMessage[] appendedRequest = [firstMessage, appendedMessage];
        int firstRawEstimateTokens = TokenEstimator.Estimate(firstRequest);
        int appendedRawEstimateTokens = TokenEstimator.Estimate(appendedRequest);
        Assert.IsGreaterThan(firstRawEstimateTokens, appendedRawEstimateTokens);

        UsageReportingChatClient provider = new(inputTokenCount: firstRawEstimateTokens - 20);
        RecordingSummarizer summarizer = new();
        ChatOptions requestOptions = new() { ModelId = "model-a" };
        CompactingChatClient client = new(provider, new AgentCompactionOptions
        {
            Reducer = new ChatReducer(
                CreateOptions(modelContextWindowTokens: appendedRawEstimateTokens + 2),
                new StaticSummaryPromptProvider("Summarize."),
                summarizer),
        });

        _ = await AgentRunAttemptContext.RunAsync(
            Guid.CreateVersion7(),
            async () =>
            {
                _ = await client.GetResponseAsync(firstRequest, requestOptions);
                _ = await client.GetResponseAsync(appendedRequest, requestOptions);
                return true;
            });

        Assert.HasCount(2, provider.Requests);
        Assert.IsEmpty(summarizer.Inputs);
        Assert.IsFalse(ContainsSummaryCheckpoint(provider.Requests[1]));
    }

    [TestMethod]
    public async Task ContextWindowFailure_ForcesCompactionOnTheNextRetryInsteadOfRepeatingRawContext()
    {
        ContextWindowThenSuccessChatClient provider = new();
        CompactingChatClient client = new(provider, new AgentCompactionOptions
        {
            Reducer = new ChatReducer(
                CreateOptions(modelContextWindowTokens: 100_000),
                new StaticSummaryPromptProvider("Summarize."),
                new StaticSummarizer()),
        });
        ChatMessage oldHistory = new(ChatRole.User, new string('x', 10_000));
        ChatMessage[] messages =
        [
            oldHistory,
            new(ChatRole.Assistant, "recent tail"),
        ];

        _ = await AgentRunAttemptContext.RunAsync(
            Guid.CreateVersion7(),
            async () =>
            {
                ModelInvocationException exception = await Assert.ThrowsExactlyAsync<ModelInvocationException>(
                    () => client.GetResponseAsync(messages));
                Assert.AreEqual(ModelInvocationFailureKind.ContextWindowExceeded, exception.FailureKind);

                _ = await client.GetResponseAsync(messages);
                return true;
            });

        Assert.HasCount(2, provider.Requests);
        Assert.AreSame(oldHistory, provider.Requests[0][0]);
        Assert.IsTrue(ContainsSummaryCheckpoint(provider.Requests[1]));
        Assert.IsFalse(provider.Requests[1].Any(message => ReferenceEquals(oldHistory, message)));
    }

    [TestMethod]
    public async Task ContextWindowFailure_WithAutomaticCompactionDisabled_StillCompactsOnRecovery()
    {
        ContextWindowThenSuccessChatClient provider = new();
        CompactingChatClient client = new(provider, new AgentCompactionOptions
        {
            Reducer = new ChatReducer(
                CreateOptions(modelContextWindowTokens: 100_000, enableAutomaticCompaction: false),
                new StaticSummaryPromptProvider("Summarize."),
                new StaticSummarizer()),
        });
        ChatMessage oldHistory = new(ChatRole.User, new string('x', 10_000));
        ChatMessage[] messages =
        [
            oldHistory,
            new(ChatRole.Assistant, "recent tail"),
        ];

        _ = await AgentRunAttemptContext.RunAsync(
            Guid.CreateVersion7(),
            async () =>
            {
                ModelInvocationException exception = await Assert.ThrowsExactlyAsync<ModelInvocationException>(
                    () => client.GetResponseAsync(messages));
                Assert.AreEqual(ModelInvocationFailureKind.ContextWindowExceeded, exception.FailureKind);

                _ = await client.GetResponseAsync(messages);
                return true;
            });

        Assert.HasCount(2, provider.Requests);
        Assert.IsTrue(ContainsSummaryCheckpoint(provider.Requests[1]));
        Assert.IsFalse(provider.Requests[1].Any(message => ReferenceEquals(oldHistory, message)));
    }

    [TestMethod]
    public async Task StreamingInitializationContextWindowFailure_ForcesCompactionOnTheNextRetry()
    {
        InitializationContextWindowThenSuccessChatClient provider = new();
        CompactingChatClient client = new(provider, new AgentCompactionOptions
        {
            Reducer = new ChatReducer(
                CreateOptions(modelContextWindowTokens: 100_000),
                new StaticSummaryPromptProvider("Summarize."),
                new StaticSummarizer()),
        });
        ChatMessage oldHistory = new(ChatRole.User, new string('x', 10_000));
        ChatMessage[] messages =
        [
            oldHistory,
            new(ChatRole.Assistant, "recent tail"),
        ];

        _ = await AgentRunAttemptContext.RunAsync(
            Guid.CreateVersion7(),
            async () =>
            {
                ModelInvocationException exception = await Assert.ThrowsExactlyAsync<ModelInvocationException>(
                    () => DrainAsync(client, messages));
                Assert.AreEqual(ModelInvocationFailureKind.ContextWindowExceeded, exception.FailureKind);

                await DrainAsync(client, messages);
                return true;
            });

        Assert.HasCount(2, provider.Requests);
        Assert.IsTrue(ContainsSummaryCheckpoint(provider.Requests[1]));
        Assert.IsFalse(provider.Requests[1].Any(message => ReferenceEquals(oldHistory, message)));
    }

    [TestMethod]
    public async Task StreamingPartialUsage_FinalInputUsageCalibratesTheNextCall()
    {
        ChatMessage[] messages = [new(ChatRole.User, new string('x', 300))];
        int rawEstimateTokens = TokenEstimator.Estimate(messages);
        PartialUsageStreamingChatClient provider = new(inputTokenCount: rawEstimateTokens + 600);
        RecordingSummarizer summarizer = new();
        CompactingChatClient client = new(provider, new AgentCompactionOptions
        {
            Reducer = new ChatReducer(
                CreateOptions(modelContextWindowTokens: rawEstimateTokens + 3),
                new StaticSummaryPromptProvider("Summarize."),
                summarizer),
        });

        _ = await AgentRunAttemptContext.RunAsync(
            Guid.CreateVersion7(),
            async () =>
            {
                await foreach (ChatResponseUpdate _ in client.GetStreamingResponseAsync(messages))
                {
                }

                _ = await client.GetResponseAsync(messages);
                return true;
            });

        Assert.HasCount(2, provider.Requests);
        Assert.HasCount(1, summarizer.Inputs);
        Assert.IsTrue(ContainsSummaryCheckpoint(provider.Requests[1]));
    }

    [TestMethod]
    public async Task StreamingPartialUsage_DoesNotClearRecoveryBeforeStreamCompletes()
    {
        PartialUsageThenFailureStreamingChatClient provider = new();
        RecordingSummarizer summarizer = new();
        CompactingChatClient client = new(provider, new AgentCompactionOptions
        {
            Reducer = new ChatReducer(
                CreateOptions(modelContextWindowTokens: 100_000),
                new StaticSummaryPromptProvider("Summarize."),
                summarizer),
        });
        ChatMessage[] messages =
        [
            new(ChatRole.User, new string('x', 10_000)),
            new(ChatRole.Assistant, "recent tail"),
        ];

        _ = await AgentRunAttemptContext.RunAsync(
            Guid.CreateVersion7(),
            async () =>
            {
                ModelInvocationException exception = await Assert.ThrowsExactlyAsync<ModelInvocationException>(
                    () => client.GetResponseAsync(messages));
                Assert.AreEqual(ModelInvocationFailureKind.ContextWindowExceeded, exception.FailureKind);

                await Assert.ThrowsExactlyAsync<IOException>(() => DrainAsync(client, messages));
                _ = await client.GetResponseAsync(messages);
                return true;
            });

        Assert.HasCount(3, provider.Requests);
        Assert.IsTrue(ContainsSummaryCheckpoint(provider.Requests[1]));
        Assert.IsTrue(ContainsSummaryCheckpoint(provider.Requests[2]));
        Assert.HasCount(2, summarizer.Inputs);
    }

    [TestMethod]
    public void DisposingAdapter_DoesNotDisposeSharedProvider()
    {
        RecordingChatClient provider = new();
        CompactingChatClient client = new(provider, new AgentCompactionOptions
        {
            Reducer = new ChatReducer(CreateOptions(), new StaticSummaryPromptProvider("Summarize."), new StaticSummarizer()),
        });

        client.Dispose();

        Assert.IsFalse(provider.IsDisposed);
    }

    private static async Task DrainAsync(
        IChatClient client,
        IReadOnlyList<ChatMessage> messages)
    {
        await foreach (ChatResponseUpdate _ in client.GetStreamingResponseAsync(messages))
        {
        }
    }

    private static CompactionOptions CreateOptions(
        long modelContextWindowTokens = 100,
        bool enableAutomaticCompaction = true) => new()
    {
        ModelContextWindowTokens = modelContextWindowTokens,
        SummaryReservedOutputTokens = 1,
        AutoCompactBufferTokens = 1,
        PreservedTailMinTokens = 1,
        PreservedTailMinMessages = 1,
        PreservedTailMaxTokens = 10_000,
        EnableAutomaticCompaction = enableAutomaticCompaction,
    };

    private sealed class StaticSummarizer : ISummarizer
    {
        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            CompactionOptions options,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult("""
                <summary>
                Current objective
                Completed work
                Next steps
                </summary>
                """);
    }

    private sealed class RecordingSummarizer : ISummarizer
    {
        public List<IReadOnlyList<ChatMessage>> Inputs { get; } = [];

        public ValueTask<string> SummarizeAsync(
            IReadOnlyList<ChatMessage> messages,
            string summaryPrompt,
            CompactionOptions options,
            CancellationToken cancellationToken)
        {
            Inputs.Add([.. messages]);
            return ValueTask.FromResult("""
                <summary>
                Current objective
                Completed work
                Next steps
                </summary>
                """);
        }
    }

    private sealed class ContextWindowThrowingSummarizer : ISummarizer
    {
        public ValueTask<string> SummarizeAsync(IReadOnlyList<ChatMessage> messages, string summaryPrompt, CompactionOptions options, CancellationToken cancellationToken) =>
            throw new HttpRequestException("HTTP 400 context_too_large");
    }

    private sealed class ToggleSummarizer : ISummarizer
    {
        public bool ShouldThrow { get; set; }
        public int CallCount { get; private set; }

        public ValueTask<string> SummarizeAsync(IReadOnlyList<ChatMessage> messages, string summaryPrompt, CompactionOptions options, CancellationToken cancellationToken)
        {
            CallCount++;
            if (ShouldThrow)
                throw new InvalidOperationException("summary failed");
            return ValueTask.FromResult("<summary>Current objective\nCompleted work\nNext steps</summary>");
        }
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];
        public bool IsDisposed { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add([.. messages]);
            ChatMessage message = Requests.Count == 1
                ? new(ChatRole.Assistant, [new FunctionCallContent("tool-1", "TestTool", new Dictionary<string, object?>())])
                : new(ChatRole.Assistant, "done");
            return Task.FromResult(new ChatResponse(message));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatResponse response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
                yield return update;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() => IsDisposed = true;
    }

    private sealed class UsageReportingChatClient(int inputTokenCount) : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add([.. messages]);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done"))
            {
                Usage = new UsageDetails
                {
                    InputTokenCount = inputTokenCount,
                    OutputTokenCount = 1,
                    TotalTokenCount = inputTokenCount + 1,
                },
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatResponse response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
                yield return update;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class ContextWindowThenSuccessChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add([.. messages]);

            if (Requests.Count == 1)
                throw new HttpRequestException("HTTP 400 context_too_large");

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ChatResponse response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
                yield return update;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class InitializationContextWindowThenSuccessChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add([.. messages]);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add([.. messages]);
            if (Requests.Count == 1)
                throw new HttpRequestException("HTTP 400 context_too_large");

            return CreateSuccessfulStream();
        }

        private static async IAsyncEnumerable<ChatResponseUpdate> CreateSuccessfulStream()
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "done");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class PartialUsageStreamingChatClient(int inputTokenCount) : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add([.. messages]);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add([.. messages]);

            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                [new UsageContent(new UsageDetails { OutputTokenCount = 1 })]);
            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                [new UsageContent(new UsageDetails
                {
                    InputTokenCount = inputTokenCount,
                    OutputTokenCount = 1,
                    TotalTokenCount = inputTokenCount + 1,
                })]);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class PartialUsageThenFailureStreamingChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add([.. messages]);
            if (Requests.Count == 1)
                throw new HttpRequestException("HTTP 400 context_too_large");

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add([.. messages]);
            return Requests.Count == 2
                ? CreatePartialFailureStream()
                : CreateSuccessfulStream();
        }

        private static async IAsyncEnumerable<ChatResponseUpdate> CreatePartialFailureStream()
        {
            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                [new UsageContent(new UsageDetails { OutputTokenCount = 1 })]);
            await Task.Yield();
            throw new IOException("stream failed after usage");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }

        private static async IAsyncEnumerable<ChatResponseUpdate> CreateSuccessfulStream()
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "done");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
