using CodeSnifferDog.Models.ReviewAgentTeam;
using System.Reactive.Subjects;

namespace CodeSnifferDog.Modules.ReviewAgentTeam;

public sealed class AgentStatusEventStream : IAgentStatusEventPublisher, IDisposable
{
    private readonly Subject<AgentStatusEvent> _innerSubject = new();
    private readonly ISubject<AgentStatusEvent> _subject;
    private bool _disposed;

    public AgentStatusEventStream()
    {
        _subject = Subject.Synchronize(_innerSubject);
    }

    public IObservable<AgentStatusEvent> Events => _subject;

    public ValueTask PublishAsync(AgentStatusEvent agentEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentEvent);
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        _subject.OnNext(agentEvent);
        return ValueTask.CompletedTask;
    }

    public void Complete()
    {
        if (_disposed)
            return;

        _disposed = true;
        _subject.OnCompleted();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _innerSubject.Dispose();
    }
}
