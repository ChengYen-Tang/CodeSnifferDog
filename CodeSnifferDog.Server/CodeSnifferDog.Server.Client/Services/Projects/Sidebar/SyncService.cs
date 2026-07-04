using CodeSnifferDog.Server.Shared.Projects;
using System.Net.Http.Json;

namespace CodeSnifferDog.Server.Client.Services.Projects.Sidebar;

/// <summary>
/// Implements sidebar synchronization, live refresh wiring, polling fallback, and project actions.
/// </summary>
public sealed class SyncService : IController, IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IRefreshSignalClient _refreshSignalClient;
    private readonly IPollingFallback _pollingFallback;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private CancellationTokenSource? _refreshCancellationTokenSource;
    private int _trailingReloadRequested;
    private bool _started;

    /// <summary>
    /// Creates the sidebar sync service from HTTP, push-refresh, and polling-fallback dependencies.
    /// </summary>
    /// <param name="httpClient">HTTP client used for snapshot reloads and project actions.</param>
    /// <param name="refreshSignalClient">Push-based refresh signal client.</param>
    /// <param name="pollingFallback">Polling fallback used when push-based refresh is unavailable.</param>
    public SyncService(
        HttpClient httpClient,
        IRefreshSignalClient refreshSignalClient,
        IPollingFallback pollingFallback)
    {
        _httpClient = httpClient;
        _refreshSignalClient = refreshSignalClient;
        _pollingFallback = pollingFallback;
    }

    /// <inheritdoc />
    public State Current { get; } = State.CreateEmpty();

    /// <inheritdoc />
    public event Action? Changed;

    /// <summary>
    /// Forces a sidebar snapshot reload.
    /// </summary>
    /// <param name="cancellationToken">Cancels the refresh.</param>
    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        ReloadAsync(isInitialLoad: false, selectedProjectIdFromUri: null, cancellationToken);

    /// <inheritdoc />
    public void InitializeSnapshot(ProjectSidebarSnapshotDto? snapshot, string? selectedProjectIdFromUri)
    {
        bool changed = Current.ApplySnapshot(snapshot, selectedProjectIdFromUri);
        changed |= Current.Transport.CompleteSnapshotLoad();
        NotifyChangedIf(changed);
    }

    /// <inheritdoc />
    public void SelectProject(string projectId)
    {
        NotifyChangedIf(Current.Ui.SelectProject(projectId));
    }

    /// <inheritdoc />
    public void ToggleGroup(string groupKey, ProjectStatus status)
    {
        NotifyChangedIf(Current.Ui.ToggleGroup(groupKey, status));
    }

    /// <inheritdoc />
    public void SyncSelectedProjectFromUri(string? selectedProjectIdFromUri)
    {
        NotifyChangedIf(Current.Ui.SyncSelectedProjectFromUri(selectedProjectIdFromUri, Current.Snapshot.Groups));
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <summary>
    /// Starts push-based live refresh and falls back to polling when startup fails.
    /// </summary>
    /// <param name="cancellationToken">Cancels live-refresh initialization.</param>
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

    /// <summary>
    /// Starts polling fallback when it is not already active.
    /// </summary>
    /// <returns><see langword="true" /> when transport state changed.</returns>
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

    /// <summary>
    /// Stops polling fallback when it is currently active.
    /// </summary>
    /// <returns><see langword="true" /> when transport state changed.</returns>
    private bool StopPollingFallback()
    {
        if (!Current.Transport.IsPollingFallbackActive && !_pollingFallback.IsActive)
            return false;

        _pollingFallback.Stop();
        return Current.Transport.SetPollingFallbackActive(_pollingFallback.IsActive);
    }

    /// <summary>
    /// Serializes reload attempts so overlapping refresh requests collapse into at most one trailing reload.
    /// </summary>
    /// <param name="isInitialLoad">Whether this reload represents the first snapshot load.</param>
    /// <param name="selectedProjectIdFromUri">Optional project identifier from the current URI.</param>
    /// <param name="cancellationToken">Cancels the reload.</param>
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

    /// <summary>
    /// Performs one snapshot reload and updates transport error/loading state.
    /// </summary>
    /// <param name="isInitialLoad">Whether this reload represents the first snapshot load.</param>
    /// <param name="selectedProjectIdFromUri">Optional project identifier from the current URI.</param>
    /// <param name="cancellationToken">Cancels the reload.</param>
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

    /// <summary>
    /// Handles connection-state callbacks from the refresh signal client.
    /// </summary>
    /// <param name="isLiveConnected">Whether push-based refresh is connected.</param>
    /// <param name="isReconnecting">Whether push-based refresh is reconnecting.</param>
    /// <param name="liveErrorMessage">Latest live connection error message.</param>
    private void OnLiveConnectionStateChanged(bool isLiveConnected, bool isReconnecting, string? liveErrorMessage)
    {
        UpdateLiveConnectionState(isLiveConnected, isReconnecting, liveErrorMessage);
    }

    /// <summary>
    /// Updates transport state and polling fallback in response to live connection changes.
    /// </summary>
    /// <param name="isLiveConnected">Whether push-based refresh is connected.</param>
    /// <param name="isReconnecting">Whether push-based refresh is reconnecting.</param>
    /// <param name="liveErrorMessage">Latest live connection error message.</param>
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

    /// <summary>
    /// Raises <see cref="Changed" /> only when state actually changed.
    /// </summary>
    /// <param name="changed">Whether state changed.</param>
    private void NotifyChangedIf(bool changed)
    {
        if (changed)
            NotifyChanged();
    }

    /// <summary>
    /// Raises the <see cref="Changed" /> event.
    /// </summary>
    private void NotifyChanged() => Changed?.Invoke();

    /// <inheritdoc />
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
