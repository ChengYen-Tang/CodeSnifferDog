namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

public interface IProjectExecutionQueueLock
{
    Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}
