using System.Net;
using System.Net.Http.Json;
using CodeSnifferDog.Server.Client.Services.Projects;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Tests.Services.Projects;

[TestClass]
public sealed class ProjectSidebarSyncServiceTests
{
    [TestMethod]
    public async Task SignalRRefreshTrigger_ReloadsSidebarSnapshotAsync()
    {
        RecordingSidebarHttpMessageHandler handler = new(
            CreateJsonResponse(CreateSnapshot("repo-after-live-refresh.zip")));
        FakeProjectSidebarRefreshSignalClient refreshSignalClient = new();
        FakeProjectSidebarPollingFallback pollingFallback = new();

        await using ProjectSidebarSyncService service = CreateService(handler, refreshSignalClient, pollingFallback);

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
        FakeProjectSidebarRefreshSignalClient refreshSignalClient = new();
        FakeProjectSidebarPollingFallback pollingFallback = new();

        await using ProjectSidebarSyncService service = CreateService(handler, refreshSignalClient, pollingFallback);

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
        FakeProjectSidebarRefreshSignalClient refreshSignalClient = new(
            startException: new InvalidOperationException("hub offline"));
        FakeProjectSidebarPollingFallback pollingFallback = new();

        await using ProjectSidebarSyncService service = CreateService(handler, refreshSignalClient, pollingFallback);

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
        FakeProjectSidebarRefreshSignalClient refreshSignalClient = new();
        FakeProjectSidebarPollingFallback pollingFallback = new();

        await using ProjectSidebarSyncService service = CreateService(handler, refreshSignalClient, pollingFallback);

        await service.StartAsync(initialSnapshot: CreateSnapshot("repo-before.zip"));

        Assert.IsTrue(service.Current.Transport.IsLiveConnected);
        Assert.IsFalse(service.Current.Transport.IsReconnecting);
        Assert.IsFalse(service.Current.Transport.IsPollingFallbackActive);
        Assert.IsFalse(pollingFallback.IsActive);
    }

    [TestMethod]
    public async Task LiveConnectionRecovered_StopsPollingFallbackAsync()
    {
        RecordingSidebarHttpMessageHandler handler = new(
            CreateJsonResponse(CreateSnapshot("repo-after-reconnect-refresh.zip")));
        FakeProjectSidebarRefreshSignalClient refreshSignalClient = new();
        FakeProjectSidebarPollingFallback pollingFallback = new();

        await using ProjectSidebarSyncService service = CreateService(handler, refreshSignalClient, pollingFallback);

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
        FakeProjectSidebarRefreshSignalClient refreshSignalClient = new();
        FakeProjectSidebarPollingFallback pollingFallback = new();

        await using ProjectSidebarSyncService service = CreateService(handler, refreshSignalClient, pollingFallback);

        await service.StartAsync(initialSnapshot: CreateSnapshot("repo-before.zip"));
        await refreshSignalClient.SetConnectionStateAsync(isLiveConnected: false, isReconnecting: true, "Live updates reconnecting...");

        Assert.IsTrue(service.Current.Transport.IsReconnecting);
        Assert.AreEqual("repo-before.zip", service.Current.Snapshot.Groups[0].Projects[0].OriginalFileName);
        Assert.IsFalse(service.Current.Transport.IsLoading);
    }

    private static ProjectSidebarSyncService CreateService(
        HttpMessageHandler handler,
        IProjectSidebarRefreshSignalClient refreshSignalClient,
        IProjectSidebarPollingFallback pollingFallback) =>
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

    private static ProjectSidebarSnapshotDto CreateSnapshot(string fileName) => new()
    {
        GeneratedAtUtc = new DateTimeOffset(2026, 5, 16, 0, 0, 0, TimeSpan.Zero),
        SelectedProjectId = Guid.Parse("70000000-0000-0000-0000-000000000701"),
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
                        Status = ProjectStatus.Reviewing,
                        CreatedAtUtc = new DateTimeOffset(2026, 5, 16, 0, 0, 0, TimeSpan.Zero),
                        SortOrder = 0,
                    }
                ],
            }
        ],
    };

    private sealed class FakeProjectSidebarRefreshSignalClient(Exception? startException = null) : IProjectSidebarRefreshSignalClient
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

    private sealed class FakeProjectSidebarPollingFallback : IProjectSidebarPollingFallback
    {
        private Func<CancellationToken, Task>? _onRefreshRequested;

        public bool IsActive { get; private set; }

        public void Start(Func<CancellationToken, Task> onRefreshRequested, CancellationToken cancellationToken)
        {
            _onRefreshRequested = onRefreshRequested;
            IsActive = true;
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
}
