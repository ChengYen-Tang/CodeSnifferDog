namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

public interface IQueueLock
{
    Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}
