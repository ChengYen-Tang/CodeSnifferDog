namespace CodeSnifferDog.Server.Client.Services.Projects;

public interface IProjectSidebarPollingFallback : IAsyncDisposable
{
    bool IsActive { get; }

    void Start(Func<CancellationToken, Task> onRefreshRequested, CancellationToken cancellationToken);

    void Stop();
}
