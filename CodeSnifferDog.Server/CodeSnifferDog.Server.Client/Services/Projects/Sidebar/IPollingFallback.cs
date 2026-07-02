namespace CodeSnifferDog.Server.Client.Services.Projects.Sidebar;

public interface IPollingFallback : IAsyncDisposable
{
    bool IsActive { get; }

    void Start(Func<CancellationToken, Task> onRefreshRequested, CancellationToken cancellationToken);

    void Stop();
}
