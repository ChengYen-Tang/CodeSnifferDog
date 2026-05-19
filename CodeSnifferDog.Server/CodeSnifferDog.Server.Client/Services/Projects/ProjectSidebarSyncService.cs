using CodeSnifferDog.Server.Shared.Projects;
using System.Net.Http.Json;

namespace CodeSnifferDog.Server.Client.Services.Projects;

public sealed class ProjectSidebarSyncService : IProjectSidebarController, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IProjectSidebarRefreshSignalClient _refreshSignalClient;
    private readonly IProjectSidebarPollingFallback _pollingFallback;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private CancellationTokenSource? _refreshCancellationTokenSource;
    private bool _started;

    public ProjectSidebarSyncService(
        HttpClient httpClient,
        IProjectSidebarRefreshSignalClient refreshSignalClient,
        IProjectSidebarPollingFallback pollingFallback)
    {
        _httpClient = httpClient;
        _refreshSignalClient = refreshSignalClient;
        _pollingFallback = pollingFallback;
    }

    public ProjectSidebarState Current { get; } = ProjectSidebarState.CreateEmpty();

    public event Action? Changed;

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        ReloadAsync(isInitialLoad: false, selectedProjectIdFromUri: null, cancellationToken);

    public void InitializeSnapshot(ProjectSidebarSnapshotDto? snapshot, string? selectedProjectIdFromUri)
    {
        Current.ApplySnapshot(snapshot, selectedProjectIdFromUri);
        Current.Transport.CompleteSnapshotLoad();
        NotifyChanged();
    }

    public void SelectProject(string projectId)
    {
        Current.Ui.SelectProject(projectId);
        NotifyChanged();
    }

    public void ToggleGroup(string groupKey, ProjectStatus status)
    {
        Current.Ui.ToggleGroup(groupKey, status);
        NotifyChanged();
    }

    public void SyncSelectedProjectFromUri(string? selectedProjectIdFromUri)
    {
        Current.Ui.SyncSelectedProjectFromUri(selectedProjectIdFromUri, Current.Snapshot.Groups);
        NotifyChanged();
    }

    public async Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _httpClient.DeleteAsync($"/api/projects/{projectId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await ReloadAsync(isInitialLoad: false, selectedProjectIdFromUri: null, cancellationToken);
            return false;
        }

        response.EnsureSuccessStatusCode();
        await ReloadAsync(isInitialLoad: false, selectedProjectIdFromUri: null, cancellationToken);
        return true;
    }

    public async Task<bool> CancelProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _httpClient.PostAsync($"/api/projects/{projectId}/cancel", content: null, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await ReloadAsync(isInitialLoad: false, selectedProjectIdFromUri: null, cancellationToken);
            return false;
        }

        response.EnsureSuccessStatusCode();
        await ReloadAsync(isInitialLoad: false, selectedProjectIdFromUri: null, cancellationToken);
        return true;
    }

    public async Task StartAsync(
        ProjectSidebarSnapshotDto? initialSnapshot = null,
        string? selectedProjectIdFromUri = null,
        CancellationToken cancellationToken = default)
    {
        if (_started)
            return;

        _started = true;
        _refreshCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (initialSnapshot is not null)
        {
            Current.ApplySnapshot(initialSnapshot, selectedProjectIdFromUri);
            Current.Transport.CompleteSnapshotLoad();
            NotifyChanged();
        }

        if (initialSnapshot is null)
            await ReloadAsync(isInitialLoad: true, selectedProjectIdFromUri, _refreshCancellationTokenSource.Token);

        await InitializeLiveRefreshAsync(_refreshCancellationTokenSource.Token);
    }

    private async Task InitializeLiveRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _refreshSignalClient.StartAsync(
                onRefreshRequested: cancellationToken => ReloadAsync(isInitialLoad: false, selectedProjectIdFromUri: null, cancellationToken),
                onConnectionStateChanged: OnLiveConnectionStateChanged,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            UpdateLiveConnectionState(isLiveConnected: false, isReconnecting: false, $"Live updates unavailable: {exception.Message}");
        }
    }

    private void StartPollingFallback()
    {
        if (_refreshCancellationTokenSource is null)
            return;

        _pollingFallback.Start(
            onRefreshRequested: pollingCancellationToken => ReloadAsync(isInitialLoad: false, selectedProjectIdFromUri: null, pollingCancellationToken),
            _refreshCancellationTokenSource.Token);
        Current.Transport.SetPollingFallbackActive(_pollingFallback.IsActive);
        NotifyChanged();
    }

    private void StopPollingFallback()
    {
        _pollingFallback.Stop();
        Current.Transport.SetPollingFallbackActive(_pollingFallback.IsActive);
        NotifyChanged();
    }

    private async Task ReloadAsync(bool isInitialLoad, string? selectedProjectIdFromUri, CancellationToken cancellationToken)
    {
        if (!await _reloadLock.WaitAsync(0, cancellationToken))
            return;

        if (isInitialLoad)
            Current.Transport.StartInitialLoad();
        else
            Current.Transport.StartRefresh();

        NotifyChanged();

        try
        {
            ProjectSidebarSnapshotDto? snapshot =
                await _httpClient.GetFromJsonAsync<ProjectSidebarSnapshotDto>("/api/projects/sidebar", cancellationToken);
            Current.ApplySnapshot(snapshot, selectedProjectIdFromUri);
            Current.Transport.CompleteSnapshotLoad();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (isInitialLoad)
                Current.Snapshot.Update(null);

            Current.Transport.CompleteSnapshotLoad($"Failed to load projects: {exception.Message}");
        }
        finally
        {
            _reloadLock.Release();
            NotifyChanged();
        }
    }

    private void OnLiveConnectionStateChanged(bool isLiveConnected, bool isReconnecting, string? liveErrorMessage)
    {
        UpdateLiveConnectionState(isLiveConnected, isReconnecting, liveErrorMessage);
    }

    private void UpdateLiveConnectionState(bool isLiveConnected, bool isReconnecting, string? liveErrorMessage)
    {
        if (isLiveConnected)
            StopPollingFallback();
        else
            StartPollingFallback();

        Current.Transport.SetReconnecting(isReconnecting);
        Current.Transport.SetLiveConnected(isLiveConnected, liveErrorMessage);
        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke();

    public async ValueTask DisposeAsync()
    {
        if (_refreshCancellationTokenSource is not null)
        {
            await _refreshCancellationTokenSource.CancelAsync();
            _refreshCancellationTokenSource.Dispose();
        }

        await _pollingFallback.DisposeAsync();
        await _refreshSignalClient.DisposeAsync();
        _reloadLock.Dispose();
    }
}
