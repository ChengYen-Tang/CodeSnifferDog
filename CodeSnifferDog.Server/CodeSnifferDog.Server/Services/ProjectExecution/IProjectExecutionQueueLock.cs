namespace CodeSnifferDog.Server.Services.ProjectExecution;

public interface IProjectExecutionQueueLock
{
    Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}
