using System.Net.Http.Json;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.SignalR.Client;

namespace CodeSnifferDog.Server.Client.Services.Projects;

public sealed class ProjectSidebarSyncService(HttpClient httpClient) : IAsyncDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan HubRetryInterval = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient = httpClient;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly SemaphoreSlim _hubLock = new(1, 1);
    private HubConnection? _hubConnection;
    private DateTimeOffset _nextHubRetryUtc = DateTimeOffset.MinValue;
    private PeriodicTimer? _refreshTimer;
    private CancellationTokenSource? _refreshCancellationTokenSource;
    private bool _started;

    public ProjectSidebarState Current { get; private set; } = new()
    {
        IsLoading = true,
    };

    public event Action? Changed;

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        ReloadAsync(isInitialLoad: false, cancellationToken);

    public async Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _httpClient.DeleteAsync($"/api/projects/{projectId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await ReloadAsync(isInitialLoad: false, cancellationToken);
            return false;
        }

        response.EnsureSuccessStatusCode();
        await ReloadAsync(isInitialLoad: false, cancellationToken);
        return true;
    }

    public async Task<bool> CancelProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _httpClient.PostAsync($"/api/projects/{projectId}/cancel", content: null, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await ReloadAsync(isInitialLoad: false, cancellationToken);
            return false;
        }

        response.EnsureSuccessStatusCode();
        await ReloadAsync(isInitialLoad: false, cancellationToken);
        return true;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
            return;

        _started = true;
        _refreshCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        StartPolling(_refreshCancellationTokenSource.Token);
        await ReloadAsync(isInitialLoad: true, _refreshCancellationTokenSource.Token);
        await InitializeHubAsync(_refreshCancellationTokenSource.Token);
    }

    private async Task InitializeHubAsync(CancellationToken cancellationToken)
    {
        if (!await _hubLock.WaitAsync(0, cancellationToken))
            return;

        try
        {
            if (_hubConnection?.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
                return;

            if (_hubConnection is null)
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(new Uri(_httpClient.BaseAddress!, ProjectUpdatesContract.HubPath))
                    .WithAutomaticReconnect()
                    .Build();
                _hubConnection.On(ProjectUpdatesContract.ProjectsChangedMethodName, async () =>
                {
                    await ReloadAsync(isInitialLoad: false, CancellationToken.None);
                });
            }

            await _hubConnection.StartAsync(cancellationToken);
            Current = new ProjectSidebarState
            {
                IsLoading = Current.IsLoading,
                ErrorMessage = Current.ErrorMessage,
                HubErrorMessage = null,
                Projects = Current.Projects,
            };
            NotifyChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _nextHubRetryUtc = DateTimeOffset.UtcNow.Add(HubRetryInterval);
            Current = new ProjectSidebarState
            {
                IsLoading = Current.IsLoading,
                ErrorMessage = Current.ErrorMessage,
                HubErrorMessage = $"Live updates unavailable: {exception.Message}",
                Projects = Current.Projects,
            };
            NotifyChanged();
        }
        finally
        {
            _hubLock.Release();
        }
    }

    private void StartPolling(CancellationToken cancellationToken)
    {
        _refreshTimer = new PeriodicTimer(RefreshInterval);
        _ = RunPollingLoopAsync(cancellationToken);
    }

    private async Task RunPollingLoopAsync(CancellationToken cancellationToken)
    {
        if (_refreshTimer is null)
            return;

        try
        {
            while (await _refreshTimer.WaitForNextTickAsync(cancellationToken))
            {
                await TryRecoverHubAsync(cancellationToken);
                await ReloadAsync(isInitialLoad: false, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task TryRecoverHubAsync(CancellationToken cancellationToken)
    {
        if (_hubConnection?.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
            return;

        if (DateTimeOffset.UtcNow < _nextHubRetryUtc)
            return;

        await InitializeHubAsync(cancellationToken);
    }

    private async Task ReloadAsync(bool isInitialLoad, CancellationToken cancellationToken)
    {
        if (!await _reloadLock.WaitAsync(0, cancellationToken))
            return;

        if (isInitialLoad)
        {
            Current = new ProjectSidebarState
            {
                IsLoading = true,
                ErrorMessage = Current.ErrorMessage,
                HubErrorMessage = Current.HubErrorMessage,
                Projects = Current.Projects,
            };
            NotifyChanged();
        }

        try
        {
            IReadOnlyList<ProjectListItemDto>? projects =
                await _httpClient.GetFromJsonAsync<IReadOnlyList<ProjectListItemDto>>("/api/projects", cancellationToken);
            Current = new ProjectSidebarState
            {
                IsLoading = false,
                ErrorMessage = null,
                HubErrorMessage = Current.HubErrorMessage,
                Projects = projects ?? [],
            };
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Current = new ProjectSidebarState
            {
                IsLoading = false,
                ErrorMessage = $"Failed to load projects: {exception.Message}",
                HubErrorMessage = Current.HubErrorMessage,
                Projects = isInitialLoad ? [] : Current.Projects,
            };
        }
        finally
        {
            _reloadLock.Release();
            NotifyChanged();
        }
    }

    private void NotifyChanged() => Changed?.Invoke();

    public async ValueTask DisposeAsync()
    {
        if (_refreshCancellationTokenSource is not null)
        {
            await _refreshCancellationTokenSource.CancelAsync();
            _refreshCancellationTokenSource.Dispose();
        }

        _refreshTimer?.Dispose();
        _reloadLock.Dispose();
        _hubLock.Dispose();

        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }
}
