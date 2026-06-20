using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

internal sealed class ProjectAgentStatusEventSubscriber : IAsyncDisposable
{
    private readonly IAgentStatusEventHandler _eventHandler;
    private readonly IDisposable _subscription;
    private readonly object _sync = new();
    private Task _processingTail = Task.CompletedTask;
    private bool _disposed;

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
}
