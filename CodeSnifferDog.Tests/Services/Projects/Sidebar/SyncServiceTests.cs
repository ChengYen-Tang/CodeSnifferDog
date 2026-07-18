using System.Net;
using System.Net.Http.Json;
using CodeSnifferDog.Server.Client.Services.Projects.Sidebar;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Tests.Services.Projects;

[TestClass]
public sealed class SyncServiceTests
{
    [TestMethod]
    public async Task EquivalentSnapshotAndSameRouteSelection_DoNotNotifyChangedAsync()
    {
        RecordingSidebarHttpMessageHandler handler = new();
        FakeRefreshSignalClient refreshSignalClient = new();
        FakePollingFallback pollingFallback = new();
        ProjectSidebarSnapshotDto snapshot = CreateSnapshot("repo-before.zip");

        await using SyncService service = CreateService(handler, refreshSignalClient, pollingFallback);
        int changedCount = 0;
        service.Changed += () => changedCount++;

        service.InitializeSnapshot(snapshot, selectedProjectIdFromUri: null);
        service.InitializeSnapshot(CreateSnapshot("repo-before.zip", generatedMinute: 1), selectedProjectIdFromUri: null);
        service.InitializeSnapshot(CreateSnapshot("repo-before.zip"), selectedProjectIdFromUri: null);
        service.SyncSelectedProjectFromUri("70000000-0000-0000-0000-000000000701");
        service.SyncSelectedProjectFromUri("70000000-0000-0000-0000-000000000701");

        Assert.AreEqual(2, changedCount);
    }

    [TestMethod]
    public async Task SidebarSelection_IsDerivedFromTheRouteAndClearsWithoutProjectUuidAsync()
    {
        RecordingSidebarHttpMessageHandler handler = new();
        FakeRefreshSignalClient refreshSignalClient = new();
        FakePollingFallback pollingFallback = new();
        ProjectSidebarSnapshotDto snapshot = CreateSnapshot("repo.zip");

        await using SyncService service = CreateService(handler, refreshSignalClient, pollingFallback);

        service.InitializeSnapshot(snapshot, selectedProjectIdFromUri: null);

        Assert.IsNull(service.Current.Ui.SelectedProjectId);

        service.SyncSelectedProjectFromUri("70000000-0000-0000-0000-000000000701");

        Assert.AreEqual("70000000-0000-0000-0000-000000000701", service.Current.Ui.SelectedProjectId);

        service.SyncSelectedProjectFromUri(null);

        Assert.IsNull(service.Current.Ui.SelectedProjectId);
    }

    [TestMethod]
    public async Task ChangedSnapshotVisibleFields_NotifyChangedAsync()
    {
        RecordingSidebarHttpMessageHandler handler = new();
        FakeRefreshSignalClient refreshSignalClient = new();
        FakePollingFallback pollingFallback = new();

        await using SyncService service = CreateService(handler, refreshSignalClient, pollingFallback);
        int changedCount = 0;
        service.Changed += () => changedCount++;

        service.InitializeSnapshot(CreateSnapshot("repo-before.zip"), selectedProjectIdFromUri: null);
        service.InitializeSnapshot(CreateSnapshot("repo-after.zip"), selectedProjectIdFromUri: null);
        service.InitializeSnapshot(CreateSnapshot("repo-after.zip", status: ProjectStatus.Completed), selectedProjectIdFromUri: null);

        Assert.AreEqual(3, changedCount);
    }

    [TestMethod]
    public async Task SignalRRefreshTrigger_ReloadsSidebarSnapshotAsync()
    {
        RecordingSidebarHttpMessageHandler handler = new(
            CreateJsonResponse(CreateSnapshot("repo-after-live-refresh.zip")));
        FakeRefreshSignalClient refreshSignalClient = new();
        FakePollingFallback pollingFallback = new();

        await using SyncService service = CreateService(handler, refreshSignalClient, pollingFallback);

        await service.StartAsync(initialSnapshot: CreateSnapshot("repo-before.zip"));
        await refreshSignalClient.TriggerRefreshAsync();

        Assert.AreEqual(1, handler.RequestCount);
        Assert.AreEqual("repo-after-live-refresh.zip", service.Current.Snapshot.Groups[0].Projects[0].OriginalFileName);
        Assert.IsTrue(service.Current.Transport.IsLiveConnected);
        Assert.IsFalse(service.Current.Transport.IsPollingFallbackActive);
    }

    [TestMethod]
    public async Task PollingFallbackTrigger_ReloadsSidebarSnapshotAsync()
    {
        RecordingSidebarHttpMessageHandler handler = new(
            CreateJsonResponse(CreateSnapshot("repo-after-polling-refresh.zip")));
        FakeRefreshSignalClient refreshSignalClient = new();
        FakePollingFallback pollingFallback = new();

        await using SyncService service = CreateService(handler, refreshSignalClient, pollingFallback);

        await service.StartAsync(initialSnapshot: CreateSnapshot("repo-before.zip"));
        await refreshSignalClient.SetConnectionStateAsync(isLiveConnected: false, isReconnecting: false, "Live updates unavailable.");
        await pollingFallback.TriggerAsync();

        Assert.AreEqual(1, handler.RequestCount);
        Assert.AreEqual("repo-after-polling-refresh.zip", service.Current.Snapshot.Groups[0].Projects[0].OriginalFileName);
        Assert.IsTrue(service.Current.Transport.IsPollingFallbackActive);
    }

    [TestMethod]
    public async Task LiveRefreshUnavailable_DoesNotBlockPollingFallbackRefreshAsync()
    {
        RecordingSidebarHttpMessageHandler handler = new(
            CreateJsonResponse(CreateSnapshot("repo-after-polling-refresh.zip")));
        FakeRefreshSignalClient refreshSignalClient = new(
            startException: new InvalidOperationException("hub offline"));
        FakePollingFallback pollingFallback = new();

        await using SyncService service = CreateService(handler, refreshSignalClient, pollingFallback);

        await service.StartAsync(initialSnapshot: CreateSnapshot("repo-before.zip"));
        await pollingFallback.TriggerAsync();

        Assert.IsFalse(service.Current.Transport.IsLiveConnected);
        Assert.AreEqual("Live updates unavailable: hub offline", service.Current.Transport.LiveErrorMessage);
        Assert.AreEqual("repo-after-polling-refresh.zip", service.Current.Snapshot.Groups[0].Projects[0].OriginalFileName);
        Assert.IsTrue(service.Current.Transport.IsPollingFallbackActive);
    }

    [TestMethod]
    public async Task LiveConnectionAvailable_DoesNotStartPollingFallbackAsync()
    {
        RecordingSidebarHttpMessageHandler handler = new();
        FakeRefreshSignalClient refreshSignalClient = new();
        FakePollingFallback pollingFallback = new();

        await using SyncService service = CreateService(handler, refreshSignalClient, pollingFallback);

        await service.StartAsync(initialSnapshot: CreateSnapshot("repo-before.zip"));

        Assert.IsTrue(service.Current.Transport.IsLiveConnected);
        Assert.IsFalse(service.Current.Transport.IsReconnecting);
        Assert.IsFalse(service.Current.Transport.IsPollingFallbackActive);
        Assert.IsFalse(pollingFallback.IsActive);
    }

    [TestMethod]
    public async Task SameLiveConnectionState_DoesNotNotifyChangedAsync()
    {
        RecordingSidebarHttpMessageHandler handler = new();
        FakeRefreshSignalClient refreshSignalClient = new();
        FakePollingFallback pollingFallback = new();

        await using SyncService service = CreateService(handler, refreshSignalClient, pollingFallback);
        int changedCount = 0;
        service.Changed += () => changedCount++;

        await service.StartAsync(initialSnapshot: CreateSnapshot("repo-before.zip"));
        await refreshSignalClient.SetConnectionStateAsync(isLiveConnected: true, isReconnecting: false, null);
        await refreshSignalClient.SetConnectionStateAsync(isLiveConnected: true, isReconnecting: false, null);

        Assert.AreEqual(2, changedCount);
        Assert.AreEqual(0, pollingFallback.StartCallCount);
    }

    [TestMethod]
    public async Task SameUnavailableLiveConnectionState_DoesNotRestartPollingFallbackAsync()
    {
        RecordingSidebarHttpMessageHandler handler = new();
        FakeRefreshSignalClient refreshSignalClient = new();
        FakePollingFallback pollingFallback = new();

        await using SyncService service = CreateService(handler, refreshSignalClient, pollingFallback);
        int changedCount = 0;
        service.Changed += () => changedCount++;

        await service.StartAsync(initialSnapshot: CreateSnapshot("repo-before.zip"));
        await refreshSignalClient.SetConnectionStateAsync(isLiveConnected: false, isReconnecting: true, "Live updates reconnecting...");
        await refreshSignalClient.SetConnectionStateAsync(isLiveConnected: false, isReconnecting: true, "Live updates reconnecting...");

        Assert.AreEqual(3, changedCount);
        Assert.AreEqual(1, pollingFallback.StartCallCount);
    }

    [TestMethod]
    public async Task LiveConnectionRecovered_StopsPollingFallbackAsync()
    {
        RecordingSidebarHttpMessageHandler handler = new(
            CreateJsonResponse(CreateSnapshot("repo-after-reconnect-refresh.zip")));
        FakeRefreshSignalClient refreshSignalClient = new();
        FakePollingFallback pollingFallback = new();

        await using SyncService service = CreateService(handler, refreshSignalClient, pollingFallback);

        await service.StartAsync(initialSnapshot: CreateSnapshot("repo-before.zip"));
        await refreshSignalClient.SetConnectionStateAsync(isLiveConnected: false, isReconnecting: true, "Live updates reconnecting...");

        Assert.IsTrue(service.Current.Transport.IsPollingFallbackActive);
        Assert.IsTrue(pollingFallback.IsActive);
        Assert.IsTrue(service.Current.Transport.IsReconnecting);

        await refreshSignalClient.SimulateReconnectRecoveredAsync();

        Assert.IsTrue(service.Current.Transport.IsLiveConnected);
        Assert.IsFalse(service.Current.Transport.IsReconnecting);
        Assert.IsFalse(service.Current.Transport.IsPollingFallbackActive);
        Assert.IsFalse(pollingFallback.IsActive);
        Assert.AreEqual(1, handler.RequestCount);
        Assert.AreEqual("repo-after-reconnect-refresh.zip", service.Current.Snapshot.Groups[0].Projects[0].OriginalFileName);
    }

    [TestMethod]
    public async Task Reconnecting_KeepsExistingSidebarSummaryAsync()
    {
        RecordingSidebarHttpMessageHandler handler = new();
        FakeRefreshSignalClient refreshSignalClient = new();
        FakePollingFallback pollingFallback = new();

        await using SyncService service = CreateService(handler, refreshSignalClient, pollingFallback);

        await service.StartAsync(initialSnapshot: CreateSnapshot("repo-before.zip"));
        await refreshSignalClient.SetConnectionStateAsync(isLiveConnected: false, isReconnecting: true, "Live updates reconnecting...");

        Assert.IsTrue(service.Current.Transport.IsReconnecting);
        Assert.AreEqual("repo-before.zip", service.Current.Snapshot.Groups[0].Projects[0].OriginalFileName);
        Assert.IsFalse(service.Current.Transport.IsLoading);
    }

    [TestMethod]
    public async Task RefreshBurstWhileReloadActive_CoalescesToActiveAndTrailingReloadAsync()
    {
        DelayedSidebarHttpMessageHandler handler = new(
            CreateSnapshot("repo-active.zip"),
            CreateSnapshot("repo-trailing.zip"));
        FakeRefreshSignalClient refreshSignalClient = new();
        FakePollingFallback pollingFallback = new();

        await using SyncService service = CreateService(handler, refreshSignalClient, pollingFallback);
        await service.StartAsync(initialSnapshot: CreateSnapshot("repo-before.zip"));

        Task firstRefresh = refreshSignalClient.TriggerRefreshAsync();
        await handler.WaitForRequestCountAsync(1);
        Task secondRefresh = refreshSignalClient.TriggerRefreshAsync();
        Task thirdRefresh = refreshSignalClient.TriggerRefreshAsync();

        handler.ReleaseNextResponse();
        await handler.WaitForRequestCountAsync(2);
        handler.ReleaseNextResponse();
        await Task.WhenAll(firstRefresh, secondRefresh, thirdRefresh);

        Assert.AreEqual(2, handler.RequestCount);
        Assert.AreEqual("repo-trailing.zip", service.Current.Snapshot.Groups[0].Projects[0].OriginalFileName);
    }

    private static SyncService CreateService(
        HttpMessageHandler handler,
        IRefreshSignalClient refreshSignalClient,
        IPollingFallback pollingFallback) =>
        new(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost"),
            },
            refreshSignalClient,
            pollingFallback);

    private static HttpResponseMessage CreateJsonResponse(ProjectSidebarSnapshotDto snapshot) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(snapshot),
        };

    private static ProjectSidebarSnapshotDto CreateSnapshot(
        string fileName,
        int generatedMinute = 0,
        ProjectStatus status = ProjectStatus.Reviewing,
        Guid? selectedProjectId = null) => new()
    {
        GeneratedAtUtc = new DateTimeOffset(2026, 5, 16, 0, generatedMinute, 0, TimeSpan.Zero),
        SelectedProjectId = selectedProjectId ?? Guid.Parse("70000000-0000-0000-0000-000000000701"),
        Groups =
        [
            new ProjectSidebarGroupDto
            {
                GroupKey = "reviewing",
                DisplayName = "Reviewing",
                Status = ProjectStatus.Reviewing,
                SortOrder = 0,
                Projects =
                [
                    new ProjectSidebarProjectDto
                    {
                        ProjectId = Guid.Parse("70000000-0000-0000-0000-000000000701"),
                        OriginalFileName = fileName,
                        Status = status,
                        CreatedAtUtc = new DateTimeOffset(2026, 5, 16, 0, 0, 0, TimeSpan.Zero),
                        SortOrder = 0,
                    }
                ],
            }
        ],
    };

    private sealed class FakeRefreshSignalClient(Exception? startException = null) : IRefreshSignalClient
    {
        private Func<CancellationToken, Task>? _onRefreshRequested;
        private Action<bool, bool, string?>? _onConnectionStateChanged;
        private readonly Exception? _startException = startException;

        public Task StartAsync(
            Func<CancellationToken, Task> onRefreshRequested,
            Action<bool, bool, string?> onConnectionStateChanged,
            CancellationToken cancellationToken = default)
        {
            if (_startException is not null)
                throw _startException;

            _onRefreshRequested = onRefreshRequested;
            _onConnectionStateChanged = onConnectionStateChanged;
            _onConnectionStateChanged.Invoke(true, false, null);
            return Task.CompletedTask;
        }

        public Task TriggerRefreshAsync() =>
            _onRefreshRequested is null ? Task.CompletedTask : _onRefreshRequested(CancellationToken.None);

        public Task SetConnectionStateAsync(bool isLiveConnected, bool isReconnecting, string? liveErrorMessage)
        {
            _onConnectionStateChanged?.Invoke(isLiveConnected, isReconnecting, liveErrorMessage);
            return Task.CompletedTask;
        }

        public async Task SimulateReconnectRecoveredAsync()
        {
            _onConnectionStateChanged?.Invoke(true, false, null);
            if (_onRefreshRequested is not null)
                await _onRefreshRequested(CancellationToken.None);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakePollingFallback : IPollingFallback
    {
        private Func<CancellationToken, Task>? _onRefreshRequested;

        public bool IsActive { get; private set; }

        public int StartCallCount { get; private set; }

        public void Start(Func<CancellationToken, Task> onRefreshRequested, CancellationToken cancellationToken)
        {
            _onRefreshRequested = onRefreshRequested;
            IsActive = true;
            StartCallCount++;
        }

        public void Stop()
        {
            IsActive = false;
        }

        public Task TriggerAsync() =>
            _onRefreshRequested is null ? Task.CompletedTask : _onRefreshRequested(CancellationToken.None);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingSidebarHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;

            if (_responses.Count == 0)
            {
                throw new AssertFailedException($"Unexpected request: {request.Method} {request.RequestUri}");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }

    private sealed class DelayedSidebarHttpMessageHandler(params ProjectSidebarSnapshotDto[] snapshots) : HttpMessageHandler
    {
        private readonly Queue<ProjectSidebarSnapshotDto> _snapshots = new(snapshots);
        private readonly Queue<TaskCompletionSource<HttpResponseMessage>> _responses = [];
        private readonly List<TaskCompletionSource> _requestWaiters = [];

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            foreach (TaskCompletionSource waiter in _requestWaiters.ToArray())
                waiter.TrySetResult();

            TaskCompletionSource<HttpResponseMessage> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _responses.Enqueue(response);
            return response.Task;
        }

        public Task WaitForRequestCountAsync(int expectedRequestCount)
        {
            if (RequestCount >= expectedRequestCount)
                return Task.CompletedTask;

            TaskCompletionSource waiter = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _requestWaiters.Add(waiter);
            return waiter.Task;
        }

        public void ReleaseNextResponse()
        {
            Assert.IsTrue(_responses.Count > 0, "No delayed sidebar response is pending.");
            Assert.IsTrue(_snapshots.Count > 0, "No sidebar snapshot response is configured.");
            _responses.Dequeue().SetResult(CreateJsonResponse(_snapshots.Dequeue()));
        }
    }
}
