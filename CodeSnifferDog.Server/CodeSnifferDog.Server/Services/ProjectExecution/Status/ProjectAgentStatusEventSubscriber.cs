using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using IProjectAgentStatusLiveUpdateNotifier = CodeSnifferDog.Server.Services.ProjectAgentStatus.IProjectAgentStatusLiveUpdateNotifier;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal sealed class ProjectAgentStatusEventSubscriber : IAsyncDisposable
{
    private readonly IAgentStatusEventHandler _eventHandler;
    private readonly IDisposable _subscription;
    private readonly object _sync = new();
    private Task _processingTail = Task.CompletedTask;
    private bool _disposed;

    public ProjectAgentStatusEventSubscriber(
        Guid projectId,
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        IProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier,
        IObservable<AgentStatusEvent> events)
        : this(
            new AgentStatusEventHandler(
                CreatePersistenceService(projectId, dbContextFactory, liveUpdateNotifier)),
            events)
    {
    }

    internal ProjectAgentStatusEventSubscriber(
        IAgentStatusEventHandler eventHandler,
        IObservable<AgentStatusEvent> events)
    {
        ArgumentNullException.ThrowIfNull(eventHandler);
        ArgumentNullException.ThrowIfNull(events);

        _eventHandler = eventHandler;
        _subscription = events.Subscribe(Enqueue);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _subscription.Dispose();
        Task tail;
        lock (_sync)
            tail = _processingTail;

        await tail.ConfigureAwait(false);
    }

    private void Enqueue(AgentStatusEvent agentEvent)
    {
        lock (_sync)
        {
            _processingTail = _processingTail
                .ContinueWith(
                    _ => _eventHandler.HandleAsync(agentEvent, CancellationToken.None),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default)
                .Unwrap();
        }
    }

    private static AgentStatusPersistenceService CreatePersistenceService(
        Guid projectId,
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        IProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(liveUpdateNotifier);

        return new AgentStatusPersistenceService(
            projectId,
            dbContextFactory,
            liveUpdateNotifier,
            new AgentStatusLiveUpdateFactory());
    }
}
