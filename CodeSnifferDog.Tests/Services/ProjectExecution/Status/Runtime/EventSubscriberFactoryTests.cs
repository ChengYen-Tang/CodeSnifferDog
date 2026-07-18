using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Tests.Services.ProjectExecution.Status.Runtime;

[TestClass]
public sealed class EventSubscriberFactoryTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task Create_UsesRuntimeFactoryHandler()
    {
        Guid projectId = Guid.CreateVersion7();
        TrackingEventHandler handler = new();
        TestRuntimeFactory runtimeFactory = new(handler);
        EventSubscriberFactory factory = new(runtimeFactory);
        using AgentStatusEventStream eventStream = new();

        await using EventSubscriber subscriber =
            factory.Create(projectId, eventStream.Events);

        await eventStream.PublishGroupCreatedAsync("group-1", "Group 1", TestContext.CancellationToken);
        eventStream.Complete();
        await subscriber.DisposeAsync();

        Assert.AreEqual(projectId, runtimeFactory.ProjectIds.Single());
        Assert.AreEqual("group-1", handler.GroupKeys.Single());
    }

    [TestMethod]
    public async Task Create_DisposeFlushesQueuedSubscriberEvents()
    {
        BlockingEventHandler handler = new();
        EventSubscriberFactory factory = new(new TestRuntimeFactory(handler));
        using AgentStatusEventStream eventStream = new();

        await using EventSubscriber subscriber =
            factory.Create(Guid.CreateVersion7(), eventStream.Events);

        await eventStream.PublishGroupCreatedAsync("group-1", "Group 1", TestContext.CancellationToken);
        await eventStream.PublishGroupCreatedAsync("group-2", "Group 2", TestContext.CancellationToken);
        await handler.FirstEventStarted.Task.WaitAsync(TestContext.CancellationToken);

        Task disposeTask = subscriber.DisposeAsync().AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.CancellationToken);

        Assert.IsFalse(disposeTask.IsCompleted);

        handler.ReleaseFirstEvent.SetResult();
        await disposeTask.WaitAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "group-1", "group-2" }, handler.GroupKeys);
    }

    private sealed class TestRuntimeFactory(IEventHandler handler) : IRuntimeFactory
    {
        public List<Guid> ProjectIds { get; } = [];

        public RuntimeContext Create(Guid projectId)
        {
            ProjectIds.Add(projectId);
            return new RuntimeContext(handler);
        }
    }

    private sealed class TrackingEventHandler : IEventHandler
    {
        public List<string> GroupKeys { get; } = [];

        public Task HandleAsync(StatusEvent agentEvent, CancellationToken cancellationToken)
        {
            if (agentEvent is GroupCreatedEvent groupCreatedEvent)
                GroupKeys.Add(groupCreatedEvent.GroupKey);

            return Task.CompletedTask;
        }
    }

    private sealed class BlockingEventHandler : IEventHandler
    {
        private int _handledCount;

        public TaskCompletionSource FirstEventStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstEvent { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> GroupKeys { get; } = [];

        public async Task HandleAsync(StatusEvent agentEvent, CancellationToken cancellationToken)
        {
            if (agentEvent is GroupCreatedEvent groupCreatedEvent)
                GroupKeys.Add(groupCreatedEvent.GroupKey);

            if (Interlocked.Increment(ref _handledCount) == 1)
            {
                FirstEventStarted.SetResult();
                await ReleaseFirstEvent.Task.WaitAsync(cancellationToken);
            }
        }
    }
}
