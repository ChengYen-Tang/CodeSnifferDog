using CodeSnifferDog.Workflows.Adapters.AgentFramework.Runtime;
using Microsoft.Agents.AI.Workflows;

namespace CodeSnifferDog.Tests.Workflows.Adapters.AgentFramework.Runtime;

[TestClass]
public sealed class WorkflowRuntimeTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunWithEventsAsync_ReturnsDomainOutputAndFrameworkEvents()
    {
        WorkflowRuntime runtime = new();
        TestRequest request = new("payload");
        int invocationCount = 0;

        WorkflowRunResult<string> result = await runtime.RunWithEventsAsync(
            executorId: "test-executor",
            input: request,
            operation: (input, cancellationToken) =>
            {
                invocationCount++;
                return Task.FromResult(input.Value + "-complete");
            },
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(1, invocationCount);
        Assert.AreEqual("payload-complete", result.Output);
        Assert.IsEmpty(result.Checkpoints);
        Assert.IsTrue(result.Events.Any(static workflowEvent =>
            workflowEvent is ExecutorInvokedEvent { ExecutorId: "test-executor" }));
        Assert.IsTrue(result.Events.Any(static workflowEvent =>
            workflowEvent is ExecutorCompletedEvent { ExecutorId: "test-executor" }));
        Assert.IsTrue(result.Events.Any(static workflowEvent =>
            workflowEvent is WorkflowOutputEvent { Data: "payload-complete" }));
    }

    [TestMethod]
    public async Task RunWithEventsAsync_WithCheckpointManager_ExposesExecutorBoundaryCheckpoint()
    {
        WorkflowRuntime runtime = new();
        CheckpointManager checkpointManager = CheckpointManager.CreateInMemory();

        WorkflowRunResult<int> result = await runtime.RunWithEventsAsync(
            executorId: "checkpointed-executor",
            input: new TestRequest("payload"),
            operation: static (_, _) => Task.FromResult(42),
            options: new WorkflowRunOptions
            {
                CheckpointManager = checkpointManager,
                SessionId = "workflow-runtime-test",
            },
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(42, result.Output);
        Assert.IsNotEmpty(result.Checkpoints);
        Assert.IsTrue(result.Events.Any(static workflowEvent =>
            workflowEvent is SuperStepCompletedEvent { CompletionInfo.Checkpoint: not null }));
    }

    [TestMethod]
    public async Task RunAsync_PropagatesTheOriginalWorkflowException()
    {
        WorkflowRuntime runtime = new();
        InvalidOperationException expected = new("workflow failed");

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => runtime.RunAsync(
                executorId: "failing-executor",
                input: new TestRequest("payload"),
                operation: (_, _) => Task.FromException<int>(expected),
                cancellationToken: TestContext.CancellationToken));

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task RunAsync_WhenCallerCancelsAfterOperationStarts_PropagatesCancellation()
    {
        WorkflowRuntime runtime = new();
        TaskCompletionSource operationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource cancellationTokenSource = new();

        Task<int> runTask = runtime.RunAsync(
            executorId: "cancellable-executor",
            input: new TestRequest("payload"),
            operation: async (_, cancellationToken) =>
            {
                operationStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return 42;
            },
            cancellationToken: cancellationTokenSource.Token);

        await operationStarted.Task.WaitAsync(TestContext.CancellationToken);
        cancellationTokenSource.Cancel();

        OperationCanceledException cancellation = await Assert.ThrowsAsync<OperationCanceledException>(
            () => runTask);

        Assert.AreEqual(cancellationTokenSource.Token, cancellation.CancellationToken);
    }

    private sealed record TestRequest(string Value);
}
