using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class AgentStatusEventSubscriberFactoryTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task Create_UsesRuntimeFactoryHandler()
    {
        Guid projectId = Guid.NewGuid();
        TrackingAgentStatusEventHandler handler = new();
        TestAgentStatusRuntimeFactory runtimeFactory = new(handler);
        AgentStatusEventSubscriberFactory factory = new(runtimeFactory);
        using AgentStatusEventStream eventStream = new();

        await using ProjectAgentStatusEventSubscriber subscriber =
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
        BlockingAgentStatusEventHandler handler = new();
        AgentStatusEventSubscriberFactory factory = new(new TestAgentStatusRuntimeFactory(handler));
        using AgentStatusEventStream eventStream = new();

        await using ProjectAgentStatusEventSubscriber subscriber =
            factory.Create(Guid.NewGuid(), eventStream.Events);

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

    private sealed class TestAgentStatusRuntimeFactory(IAgentStatusEventHandler handler) : IAgentStatusRuntimeFactory
    {
        public List<Guid> ProjectIds { get; } = [];

        public AgentStatusRuntime Create(Guid projectId)
        {
            ProjectIds.Add(projectId);
            return new AgentStatusRuntime(handler);
        }
    }

    private sealed class TrackingAgentStatusEventHandler : IAgentStatusEventHandler
    {
        public List<string> GroupKeys { get; } = [];

        public Task HandleAsync(AgentStatusEvent agentEvent, CancellationToken cancellationToken)
        {
            if (agentEvent is AgentGroupCreatedEvent groupCreatedEvent)
                GroupKeys.Add(groupCreatedEvent.GroupKey);

            return Task.CompletedTask;
        }
    }

    private sealed class BlockingAgentStatusEventHandler : IAgentStatusEventHandler
    {
        private int _handledCount;

        public TaskCompletionSource FirstEventStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseFirstEvent { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> GroupKeys { get; } = [];

        public async Task HandleAsync(AgentStatusEvent agentEvent, CancellationToken cancellationToken)
        {
            if (agentEvent is AgentGroupCreatedEvent groupCreatedEvent)
                GroupKeys.Add(groupCreatedEvent.GroupKey);

            if (Interlocked.Increment(ref _handledCount) == 1)
            {
                FirstEventStarted.SetResult();
                await ReleaseFirstEvent.Task.WaitAsync(cancellationToken);
            }
        }
    }
}
