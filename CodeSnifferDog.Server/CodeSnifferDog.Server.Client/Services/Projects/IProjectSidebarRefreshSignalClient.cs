namespace CodeSnifferDog.Server.Client.Services.Projects;

public interface IProjectSidebarRefreshSignalClient : IAsyncDisposable
{
    Task StartAsync(
        Func<CancellationToken, Task> onRefreshRequested,
        Action<bool, bool, string?> onConnectionStateChanged,
        CancellationToken cancellationToken = default);
}
