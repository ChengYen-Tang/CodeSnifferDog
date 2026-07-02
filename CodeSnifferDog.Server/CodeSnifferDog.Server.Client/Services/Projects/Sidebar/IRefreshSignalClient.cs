namespace CodeSnifferDog.Server.Client.Services.Projects.Sidebar;

public interface IRefreshSignalClient : IAsyncDisposable
{
    Task StartAsync(
        Func<CancellationToken, Task> onRefreshRequested,
        Action<bool, bool, string?> onConnectionStateChanged,
        CancellationToken cancellationToken = default);
}
