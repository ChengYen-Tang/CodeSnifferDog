using CodeSnifferDog.Server.Shared.Projects;
using System.Net.Http.Json;

namespace CodeSnifferDog.Server.Client.Services.Projects.Sidebar;

public sealed class SyncService : IController, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IRefreshSignalClient _refreshSignalClient;
    private readonly IPollingFallback _pollingFallback;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private CancellationTokenSource? _refreshCancellationTokenSource;
    private int _trailingReloadRequested;
    private bool _started;

    public SyncService(
        HttpClient httpClient,
        IRefreshSignalClient refreshSignalClient,
        IPollingFallback pollingFallback)
    {
        _httpClient = httpClient;
        _refreshSignalClient = refreshSignalClient;
        _pollingFallback = pollingFallback;
    }

    public State Current { get; } = State.CreateEmpty();

    public event Action? Changed;

    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        ReloadAsync(isInitialLoad: false, selectedProjectIdFromUri: null, cancellationToken);

    public void InitializeSnapshot(ProjectSidebarSnapshotDto? snapshot, string? selectedProjectIdFromUri)
    {
        bool changed = Current.ApplySnapshot(snapshot, selectedProjectIdFromUri);
        changed |= Current.Transport.CompleteSnapshotLoad();
        NotifyChangedIf(changed);
    }

    public void SelectProject(string projectId)
    {
        NotifyChangedIf(Current.Ui.SelectProject(projectId));
    }

    public void ToggleGroup(string groupKey, ProjectStatus status)
    {
        NotifyChangedIf(Current.Ui.ToggleGroup(groupKey, status));
    }

    public void SyncSelectedProjectFromUri(string? selectedProjectIdFromUri)
    {
        NotifyChangedIf(Current.Ui.SyncSelectedProjectFromUri(selectedProjectIdFromUri, Current.Snapshot.Groups));
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
            bool changed = Current.ApplySnapshot(initialSnapshot, selectedProjectIdFromUri);
            changed |= Current.Transport.CompleteSnapshotLoad();
            NotifyChangedIf(changed);
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

    private bool StartPollingFallback()
    {
        if (_refreshCancellationTokenSource is null)
            return false;

        if (Current.Transport.IsPollingFallbackActive && _pollingFallback.IsActive)
            return false;

        _pollingFallback.Start(
            onRefreshRequested: pollingCancellationToken => ReloadAsync(isInitialLoad: false, selectedProjectIdFromUri: null, pollingCancellationToken),
            _refreshCancellationTokenSource.Token);
        return Current.Transport.SetPollingFallbackActive(_pollingFallback.IsActive);
    }

    private bool StopPollingFallback()
    {
        if (!Current.Transport.IsPollingFallbackActive && !_pollingFallback.IsActive)
            return false;

        _pollingFallback.Stop();
        return Current.Transport.SetPollingFallbackActive(_pollingFallback.IsActive);
    }

    private async Task ReloadAsync(bool isInitialLoad, string? selectedProjectIdFromUri, CancellationToken cancellationToken)
    {
        if (!await _reloadLock.WaitAsync(0, cancellationToken))
        {
            if (!isInitialLoad)
                Interlocked.Exchange(ref _trailingReloadRequested, 1);

            return;
        }

        try
        {
            bool runTrailingReload;
            do
            {
                Interlocked.Exchange(ref _trailingReloadRequested, 0);
                await ReloadOnceAsync(isInitialLoad, selectedProjectIdFromUri, cancellationToken).ConfigureAwait(false);
                isInitialLoad = false;
                selectedProjectIdFromUri = null;
                runTrailingReload = Interlocked.Exchange(ref _trailingReloadRequested, 0) == 1;
            }
            while (runTrailingReload);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private async Task ReloadOnceAsync(bool isInitialLoad, string? selectedProjectIdFromUri, CancellationToken cancellationToken)
    {
        bool changed = isInitialLoad
            ? Current.Transport.StartInitialLoad()
            : Current.Transport.StartRefresh();

        NotifyChangedIf(changed);

        try
        {
            ProjectSidebarSnapshotDto? snapshot =
                await _httpClient.GetFromJsonAsync<ProjectSidebarSnapshotDto>("/api/projects/sidebar", cancellationToken);
            changed = Current.ApplySnapshot(snapshot, selectedProjectIdFromUri);
            changed |= Current.Transport.CompleteSnapshotLoad();
        }
        catch (OperationCanceledException)
        {
            changed = false;
        }
        catch (Exception exception)
        {
            if (isInitialLoad)
                changed = Current.Snapshot.Update(null);

            changed |= Current.Transport.CompleteSnapshotLoad($"Failed to load projects: {exception.Message}");
        }

        NotifyChangedIf(changed);
    }

    private void OnLiveConnectionStateChanged(bool isLiveConnected, bool isReconnecting, string? liveErrorMessage)
    {
        UpdateLiveConnectionState(isLiveConnected, isReconnecting, liveErrorMessage);
    }

    private void UpdateLiveConnectionState(bool isLiveConnected, bool isReconnecting, string? liveErrorMessage)
    {
        bool changed;
        if (isLiveConnected)
            changed = StopPollingFallback();
        else
            changed = StartPollingFallback();

        changed |= Current.Transport.SetReconnecting(isReconnecting);
        changed |= Current.Transport.SetLiveConnected(isLiveConnected, liveErrorMessage);
        NotifyChangedIf(changed);
    }

    private void NotifyChangedIf(bool changed)
    {
        if (changed)
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
