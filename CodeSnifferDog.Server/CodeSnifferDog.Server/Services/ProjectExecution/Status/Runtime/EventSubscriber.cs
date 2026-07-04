using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

/// <summary>
/// Subscribes to a status event stream and processes each event sequentially.
/// </summary>
internal sealed class EventSubscriber : IAsyncDisposable
{
    private readonly IEventHandler _eventHandler;
    private readonly IDisposable _subscription;
    private readonly object _sync = new();
    private Task _processingTail = Task.CompletedTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventSubscriber"/> class.
    /// </summary>
    /// <param name="eventHandler">Handler that persists each incoming status event.</param>
    /// <param name="events">Observable stream of status events to subscribe to.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="eventHandler"/> or <paramref name="events"/> is <see langword="null"/>.
    /// </exception>
    internal EventSubscriber(
        IEventHandler eventHandler,
        IObservable<StatusEvent> events)
    {
        ArgumentNullException.ThrowIfNull(eventHandler);
        ArgumentNullException.ThrowIfNull(events);

        _eventHandler = eventHandler;
        _subscription = events.Subscribe(Enqueue);
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Enqueues a status event behind any event currently being processed.
    /// </summary>
    /// <param name="agentEvent">Status event to process.</param>
    private void Enqueue(StatusEvent agentEvent)
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
