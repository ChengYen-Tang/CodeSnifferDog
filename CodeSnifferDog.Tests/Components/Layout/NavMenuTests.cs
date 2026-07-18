using Bunit;
using System.Net;
using System.Net.Http.Json;
using CodeSnifferDog.Server.Client.Layout;
using CodeSnifferDog.Server.Client.Services.Projects.Sidebar;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSnifferDog.Tests.Components.Layout;

[TestClass]
public sealed class NavMenuTests
{
    [TestMethod]
    public void BrandLink_NavigatesToGitHubRepository()
    {
        using Bunit.TestContext context = new();
        RegisterSidebarServices(context, new ThrowingHttpMessageHandler());
        context.Renderer.SetRendererInfo(new RendererInfo("Static", isInteractive: false));

        IRenderedComponent<NavMenu> cut = context.RenderComponent<NavMenu>();

        AngleSharp.Dom.IElement brandLink = cut.Find(".brand-main");
        Assert.AreEqual("a", brandLink.TagName.ToLowerInvariant());
        Assert.AreEqual("https://github.com/ChengYen-Tang/CodeSnifferDog", brandLink.GetAttribute("href"));
        Assert.AreEqual("_blank", brandLink.GetAttribute("target"));
        Assert.AreEqual("noopener noreferrer", brandLink.GetAttribute("rel"));
    }

    [TestMethod]
    public void InitialSnapshot_RendersSidebarWithoutClientReload()
    {
        using Bunit.TestContext context = new();
        RegisterSidebarServices(context, new ThrowingHttpMessageHandler());
        context.Renderer.SetRendererInfo(new RendererInfo("Static", isInteractive: false));

        ProjectSidebarSnapshotDto snapshot = new()
        {
            GeneratedAtUtc = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero),
            SelectedProjectId = Guid.Parse("70000000-0000-0000-0000-000000000401"),
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
                            ProjectId = Guid.Parse("70000000-0000-0000-0000-000000000401"),
                            OriginalFileName = "repo-a.zip",
                            Status = ProjectStatus.Reviewing,
                            CreatedAtUtc = new DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero),
                            SortOrder = 0,
                        }
                    ],
                },
                new ProjectSidebarGroupDto
                {
                    GroupKey = "completed",
                    DisplayName = "Completed",
                    Status = ProjectStatus.Completed,
                    SortOrder = 1,
                    Projects = [],
                },
                new ProjectSidebarGroupDto
                {
                    GroupKey = "queued",
                    DisplayName = "Queued",
                    Status = ProjectStatus.Queued,
                    SortOrder = 2,
                    Projects = [],
                },
                new ProjectSidebarGroupDto
                {
                    GroupKey = "failed",
                    DisplayName = "Failed",
                    Status = ProjectStatus.Failed,
                    SortOrder = 3,
                    Projects = [],
                },
                new ProjectSidebarGroupDto
                {
                    GroupKey = "canceled",
                    DisplayName = "Canceled",
                    Status = ProjectStatus.Canceled,
                    SortOrder = 4,
                    Projects = [],
                }
            ],
        };

        IRenderedComponent<NavMenu> cut = context.RenderComponent<NavMenu>(
            parameters => parameters.Add(component => component.InitialSnapshot, snapshot));

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "repo-a.zip");
            StringAssert.Contains(cut.Markup, "Reviewing");
            Assert.IsEmpty(cut.FindAll(".sidebar-status-message"));
            Assert.AreEqual("div", cut.Find(".project-main").TagName.ToLowerInvariant());
            Assert.IsEmpty(cut.FindAll(".project-link.active"));
        });
    }

    [TestMethod]
    public void RefreshKeepsRouteSelectedProjectWhenItStillExists()
    {
        using Bunit.TestContext context = new();
        RegisterSidebarServices(context, new ThrowingHttpMessageHandler());
        context.Renderer.SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        SyncService sidebarSyncService = context.Services.GetRequiredService<SyncService>();

        Guid projectAId = Guid.Parse("70000000-0000-0000-0000-000000000411");
        Guid projectBId = Guid.Parse("70000000-0000-0000-0000-000000000412");

        ProjectSidebarSnapshotDto initialSnapshot = CreateSnapshot(
            selectedProjectId: projectAId,
            CreateReviewingGroup(projectAId, "repo-a.zip", projectBId, "repo-b.zip"));

        IRenderedComponent<NavMenu> cut = context.RenderComponent<NavMenu>(
            parameters => parameters.Add(component => component.InitialSnapshot, initialSnapshot));

        cut.InvokeAsync(() => sidebarSyncService.SyncSelectedProjectFromUri(projectBId.ToString())).GetAwaiter().GetResult();

        ProjectSidebarSnapshotDto refreshedSnapshot = CreateSnapshot(
            selectedProjectId: projectAId,
            CreateReviewingGroup(projectAId, "repo-a.zip", projectBId, "repo-b.zip"));

        cut.InvokeAsync(() =>
        {
            sidebarSyncService.InitializeSnapshot(refreshedSnapshot, selectedProjectIdFromUri: null);
        }).GetAwaiter().GetResult();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(1, cut.FindAll(".project-link.active").Count);
            StringAssert.Contains(cut.Find(".project-link.active").TextContent, "repo-b.zip");
        });
    }

    [TestMethod]
    public void RefreshKeepsCollapsedGroupStateForExistingGroup()
    {
        using Bunit.TestContext context = new();
        RegisterSidebarServices(context, new ThrowingHttpMessageHandler());
        context.Renderer.SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        SyncService sidebarSyncService = context.Services.GetRequiredService<SyncService>();

        Guid projectAId = Guid.Parse("70000000-0000-0000-0000-000000000421");
        Guid projectBId = Guid.Parse("70000000-0000-0000-0000-000000000422");

        ProjectSidebarSnapshotDto initialSnapshot = CreateSnapshot(
            selectedProjectId: projectAId,
            CreateReviewingGroup(projectAId, "repo-a.zip", projectBId, "repo-b.zip"));

        IRenderedComponent<NavMenu> cut = context.RenderComponent<NavMenu>(
            parameters => parameters.Add(component => component.InitialSnapshot, initialSnapshot));

        cut.Find(".status-summary").Click();

        cut.WaitForAssertion(() => Assert.AreEqual(0, cut.FindAll(".project-link").Count));

        ProjectSidebarSnapshotDto refreshedSnapshot = CreateSnapshot(
            selectedProjectId: projectAId,
            CreateReviewingGroup(projectAId, "repo-a.zip", projectBId, "repo-b.zip"));

        cut.InvokeAsync(() =>
        {
            sidebarSyncService.InitializeSnapshot(refreshedSnapshot, selectedProjectIdFromUri: null);
        }).GetAwaiter().GetResult();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(0, cut.FindAll(".project-link").Count);
            StringAssert.Contains(cut.Find(".group-chevron").TextContent, ">");
        });
    }

    [TestMethod]
    public void RefreshClearsRouteSelectionWhenCurrentProjectDisappears()
    {
        using Bunit.TestContext context = new();
        RegisterSidebarServices(context, new ThrowingHttpMessageHandler());
        context.Renderer.SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        SyncService sidebarSyncService = context.Services.GetRequiredService<SyncService>();

        Guid projectAId = Guid.Parse("70000000-0000-0000-0000-000000000431");
        Guid projectBId = Guid.Parse("70000000-0000-0000-0000-000000000432");
        Guid projectCId = Guid.Parse("70000000-0000-0000-0000-000000000433");

        ProjectSidebarSnapshotDto initialSnapshot = CreateSnapshot(
            selectedProjectId: projectAId,
            CreateReviewingGroup(projectAId, "repo-a.zip", projectBId, "repo-b.zip"));

        IRenderedComponent<NavMenu> cut = context.RenderComponent<NavMenu>(
            parameters => parameters.Add(component => component.InitialSnapshot, initialSnapshot));

        cut.InvokeAsync(() => sidebarSyncService.SyncSelectedProjectFromUri(projectBId.ToString())).GetAwaiter().GetResult();

        ProjectSidebarSnapshotDto refreshedSnapshot = CreateSnapshot(
            selectedProjectId: projectCId,
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
                        ProjectId = projectCId,
                        OriginalFileName = "repo-c.zip",
                        Status = ProjectStatus.Reviewing,
                        CreatedAtUtc = new DateTimeOffset(2026, 5, 15, 11, 10, 0, TimeSpan.Zero),
                        SortOrder = 0,
                    }
                ],
            });

        cut.InvokeAsync(() =>
        {
            sidebarSyncService.InitializeSnapshot(refreshedSnapshot, selectedProjectIdFromUri: null);
        }).GetAwaiter().GetResult();

        cut.WaitForAssertion(() =>
        {
            Assert.IsEmpty(cut.FindAll(".project-link.active"));
        });
    }

    [TestMethod]
    public void RefreshRemovesExpansionStateForMissingGroup()
    {
        using Bunit.TestContext context = new();
        RegisterSidebarServices(context, new ThrowingHttpMessageHandler());
        context.Renderer.SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        SyncService sidebarSyncService = context.Services.GetRequiredService<SyncService>();

        Guid reviewingProjectId = Guid.Parse("70000000-0000-0000-0000-000000000441");
        Guid completedProjectId = Guid.Parse("70000000-0000-0000-0000-000000000442");

        ProjectSidebarSnapshotDto initialSnapshot = CreateSnapshot(
            selectedProjectId: reviewingProjectId,
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
                        ProjectId = reviewingProjectId,
                        OriginalFileName = "repo-reviewing.zip",
                        Status = ProjectStatus.Reviewing,
                        CreatedAtUtc = new DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero),
                        SortOrder = 0,
                    }
                ],
            },
            new ProjectSidebarGroupDto
            {
                GroupKey = "completed",
                DisplayName = "Completed",
                Status = ProjectStatus.Completed,
                SortOrder = 1,
                Projects =
                [
                    new ProjectSidebarProjectDto
                    {
                        ProjectId = completedProjectId,
                        OriginalFileName = "repo-completed.zip",
                        Status = ProjectStatus.Completed,
                        CreatedAtUtc = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero),
                        SortOrder = 0,
                    }
                ],
            });

        IRenderedComponent<NavMenu> cut = context.RenderComponent<NavMenu>(
            parameters => parameters.Add(component => component.InitialSnapshot, initialSnapshot));

        cut.FindAll(".status-summary")[1].Click();

        cut.WaitForAssertion(() => Assert.AreEqual(1, cut.FindAll(".project-link").Count));

        ProjectSidebarSnapshotDto refreshedSnapshot = CreateSnapshot(
            selectedProjectId: reviewingProjectId,
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
                        ProjectId = reviewingProjectId,
                        OriginalFileName = "repo-reviewing.zip",
                        Status = ProjectStatus.Reviewing,
                        CreatedAtUtc = new DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero),
                        SortOrder = 0,
                    }
                ],
            });

        cut.InvokeAsync(() =>
        {
            sidebarSyncService.InitializeSnapshot(refreshedSnapshot, selectedProjectIdFromUri: null);
        }).GetAwaiter().GetResult();

        cut.WaitForAssertion(() =>
        {
            CollectionAssert.AreEqual(
                new[] { "Reviewing" },
                cut.FindAll(".group-title").Select(node => node.TextContent).ToList());
            Assert.AreEqual(1, cut.FindAll(".project-link").Count);
        });
    }

    [TestMethod]
    public void Reconnecting_KeepsExistingSidebarSummaryVisible()
    {
        using Bunit.TestContext context = new();
        RegisterSidebarServices(context, new ThrowingHttpMessageHandler());
        context.Renderer.SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        SyncService sidebarSyncService = context.Services.GetRequiredService<SyncService>();

        Guid projectId = Guid.Parse("70000000-0000-0000-0000-000000000451");
        ProjectSidebarSnapshotDto initialSnapshot = CreateSnapshot(
            selectedProjectId: projectId,
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
                        ProjectId = projectId,
                        OriginalFileName = "repo-reconnect.zip",
                        Status = ProjectStatus.Reviewing,
                        CreatedAtUtc = new DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero),
                        SortOrder = 0,
                    }
                ],
            });

        IRenderedComponent<NavMenu> cut = context.RenderComponent<NavMenu>(
            parameters => parameters.Add(component => component.InitialSnapshot, initialSnapshot));

        cut.InvokeAsync(() =>
        {
            sidebarSyncService.Current.Transport.SetReconnecting(true);
            sidebarSyncService.Current.Transport.SetLiveConnected(false, "Live updates reconnecting...");
            cut.Render();
        }).GetAwaiter().GetResult();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "repo-reconnect.zip");
            StringAssert.Contains(cut.Markup, "Live updates reconnecting...");
            Assert.AreEqual(1, cut.FindAll(".project-link").Count);
        });
    }

    [TestMethod]
    public void InteractiveRefreshSignal_ReloadsSidebarThroughFullNavMenuFlow()
    {
        using Bunit.TestContext context = new();
        ControlledRefreshSignalClient refreshSignalClient = new();
        RegisterSidebarServices(
            context,
            new SidebarSnapshotMessageHandler(
            CreateSnapshot(
                selectedProjectId: Guid.Parse("70000000-0000-0000-0000-000000000461"),
                CreateReviewingGroup(
                    Guid.Parse("70000000-0000-0000-0000-000000000461"),
                    "repo-after-status-change.zip",
                    Guid.Parse("70000000-0000-0000-0000-000000000462"),
                    "repo-secondary.zip"))),
            refreshSignalClient);
        context.Renderer.SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));

        ProjectSidebarSnapshotDto initialSnapshot = CreateSnapshot(
            selectedProjectId: Guid.Parse("70000000-0000-0000-0000-000000000460"),
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
                        ProjectId = Guid.Parse("70000000-0000-0000-0000-000000000460"),
                        OriginalFileName = "repo-before-status-change.zip",
                        Status = ProjectStatus.Reviewing,
                        CreatedAtUtc = new DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero),
                        SortOrder = 0,
                    }
                ],
            });

        IRenderedComponent<NavMenu> cut = context.RenderComponent<NavMenu>(
            parameters => parameters.Add(component => component.InitialSnapshot, initialSnapshot));

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "repo-before-status-change.zip"));

        refreshSignalClient.TriggerRefresh();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "repo-after-status-change.zip");
            Assert.DoesNotContain(cut.Markup, "repo-before-status-change.zip");
        });
    }

    [TestMethod]
    public void InteractiveReconnect_RehydratesWithoutBlankingSidebar()
    {
        using Bunit.TestContext context = new();
        ControlledRefreshSignalClient refreshSignalClient = new();
        RegisterSidebarServices(
            context,
            new SidebarSnapshotMessageHandler(
            CreateSnapshot(
                selectedProjectId: Guid.Parse("70000000-0000-0000-0000-000000000471"),
                new ProjectSidebarGroupDto
                {
                    GroupKey = "completed",
                    DisplayName = "Completed",
                    Status = ProjectStatus.Completed,
                    SortOrder = 1,
                    Projects =
                    [
                        new ProjectSidebarProjectDto
                        {
                            ProjectId = Guid.Parse("70000000-0000-0000-0000-000000000471"),
                            OriginalFileName = "repo-after-reconnect.zip",
                            Status = ProjectStatus.Completed,
                            CreatedAtUtc = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero),
                            SortOrder = 0,
                        }
                    ],
                })),
            refreshSignalClient);
        context.Renderer.SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));

        ProjectSidebarSnapshotDto initialSnapshot = CreateSnapshot(
            selectedProjectId: Guid.Parse("70000000-0000-0000-0000-000000000470"),
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
                        ProjectId = Guid.Parse("70000000-0000-0000-0000-000000000470"),
                        OriginalFileName = "repo-before-reconnect.zip",
                        Status = ProjectStatus.Reviewing,
                        CreatedAtUtc = new DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero),
                        SortOrder = 0,
                    }
                ],
            });

        IRenderedComponent<NavMenu> cut = context.RenderComponent<NavMenu>(
            parameters => parameters.Add(component => component.InitialSnapshot, initialSnapshot));

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "repo-before-reconnect.zip"));

        refreshSignalClient.TriggerReconnecting();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "repo-before-reconnect.zip");
            StringAssert.Contains(cut.Markup, "Live updates reconnecting...");
        });

        refreshSignalClient.TriggerReconnectRecovered();

        cut.WaitForAssertion(() =>
        {
            StringAssert.Contains(cut.Markup, "repo-after-reconnect.zip");
            Assert.DoesNotContain(cut.Markup, "repo-before-reconnect.zip");
            Assert.IsEmpty(cut.FindAll(".sidebar-status-error"));
        });
    }

    [TestMethod]
    public void LargeSidebarSnapshot_RendersProjectsAndRefreshesCachedProjection()
    {
        using Bunit.TestContext context = new();
        RegisterSidebarServices(context, new ThrowingHttpMessageHandler());
        context.Renderer.SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        SyncService sidebarSyncService = context.Services.GetRequiredService<SyncService>();
        Guid selectedProjectId = Guid.Parse("70000000-0000-0000-0001-000000000001");
        ProjectSidebarSnapshotDto initialSnapshot = CreateSnapshot(
            selectedProjectId,
            CreateLargeReviewingGroup("repo-large", 100));

        IRenderedComponent<NavMenu> cut = context.RenderComponent<NavMenu>(
            parameters => parameters.Add(component => component.InitialSnapshot, initialSnapshot));

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(100, cut.FindAll(".project-link").Count);
            Assert.IsEmpty(cut.FindAll(".project-link.active"));
            StringAssert.Contains(cut.Markup, "repo-large-100.zip");
        });

        ProjectSidebarSnapshotDto refreshedSnapshot = CreateSnapshot(
            Guid.Parse("70000000-0000-0000-0001-000000000050"),
            CreateLargeReviewingGroup("repo-refreshed", 100));

        cut.InvokeAsync(() =>
        {
            sidebarSyncService.InitializeSnapshot(refreshedSnapshot, selectedProjectIdFromUri: null);
        }).GetAwaiter().GetResult();

        cut.WaitForAssertion(() =>
        {
            Assert.AreEqual(100, cut.FindAll(".project-link").Count);
            StringAssert.Contains(cut.Markup, "repo-refreshed-100.zip");
            Assert.DoesNotContain(cut.Markup, "repo-large-100.zip");
        });
    }

    [TestMethod]
    public void NoOpSidebarUpdates_DoNotRerenderNavigation()
    {
        using Bunit.TestContext context = new();
        RegisterSidebarServices(context, new ThrowingHttpMessageHandler());
        context.Renderer.SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        SyncService sidebarSyncService = context.Services.GetRequiredService<SyncService>();
        Guid selectedProjectId = Guid.Parse("70000000-0000-0000-0000-000000000481");
        ProjectSidebarSnapshotDto initialSnapshot = CreateSnapshot(
            selectedProjectId,
            CreateReviewingGroup(
                selectedProjectId,
                "repo-noop.zip",
                Guid.Parse("70000000-0000-0000-0000-000000000482"),
                "repo-secondary.zip"));

        IRenderedComponent<NavMenu> cut = context.RenderComponent<NavMenu>(
            parameters => parameters.Add(component => component.InitialSnapshot, initialSnapshot));
        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "repo-noop.zip"));
        int renderCount = cut.RenderCount;
        string markup = cut.Markup;

        cut.InvokeAsync(() =>
        {
            sidebarSyncService.InitializeSnapshot(CreateSnapshot(
                selectedProjectId,
                CreateReviewingGroup(
                    selectedProjectId,
                    "repo-noop.zip",
                    Guid.Parse("70000000-0000-0000-0000-000000000482"),
                    "repo-secondary.zip")), selectedProjectIdFromUri: null);
            sidebarSyncService.SyncSelectedProjectFromUri(null);
        }).GetAwaiter().GetResult();

        Assert.AreEqual(renderCount, cut.RenderCount);
        Assert.AreEqual(markup, cut.Markup);
    }

    private static ProjectSidebarSnapshotDto CreateSnapshot(Guid? selectedProjectId, params ProjectSidebarGroupDto[] groups) => new()
    {
        GeneratedAtUtc = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero),
        SelectedProjectId = selectedProjectId,
        Groups = groups,
    };

    private static void RegisterSidebarServices(
        Bunit.TestContext context,
        HttpMessageHandler messageHandler,
        IRefreshSignalClient? refreshSignalClient = null)
    {
        context.Services.AddSingleton(new HttpClient(messageHandler)
        {
            BaseAddress = new Uri("http://localhost"),
        });

        context.Services.AddSingleton<IRefreshSignalClient>(
            refreshSignalClient ?? new StubRefreshSignalClient());
        context.Services.AddSingleton<IPollingFallback, StubPollingFallback>();
        context.Services.AddSingleton<SyncService>();
        context.Services.AddSingleton<IController>(serviceProvider =>
            serviceProvider.GetRequiredService<SyncService>());
    }

    private static ProjectSidebarGroupDto CreateReviewingGroup(
        Guid projectAId,
        string projectAName,
        Guid projectBId,
        string projectBName) => new()
        {
            GroupKey = "reviewing",
            DisplayName = "Reviewing",
            Status = ProjectStatus.Reviewing,
            SortOrder = 0,
            Projects =
            [
                new ProjectSidebarProjectDto
                {
                    ProjectId = projectAId,
                    OriginalFileName = projectAName,
                    Status = ProjectStatus.Reviewing,
                    CreatedAtUtc = new DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero),
                    SortOrder = 0,
                },
                new ProjectSidebarProjectDto
                {
                    ProjectId = projectBId,
                    OriginalFileName = projectBName,
                    Status = ProjectStatus.Reviewing,
                    CreatedAtUtc = new DateTimeOffset(2026, 5, 15, 11, 5, 0, TimeSpan.Zero),
                    SortOrder = 1,
                }
            ],
        };

    private static ProjectSidebarGroupDto CreateLargeReviewingGroup(string namePrefix, int projectCount) => new()
    {
        GroupKey = "reviewing",
        DisplayName = "Reviewing",
        Status = ProjectStatus.Reviewing,
        SortOrder = 0,
        Projects = Enumerable.Range(1, projectCount)
            .Select(index => new ProjectSidebarProjectDto
            {
                ProjectId = Guid.Parse($"70000000-0000-0000-0001-{index:000000000000}"),
                OriginalFileName = $"{namePrefix}-{index:000}.zip",
                Status = ProjectStatus.Reviewing,
                CreatedAtUtc = new DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero).AddMinutes(index),
                SortOrder = index,
            })
            .ToList(),
    };

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new AssertFailedException($"HTTP should not be called during non-interactive initial render. Request: {request.RequestUri}");
    }

    private sealed class StubRefreshSignalClient : IRefreshSignalClient
    {
        public Task StartAsync(
            Func<CancellationToken, Task> onRefreshRequested,
            Action<bool, bool, string?> onConnectionStateChanged,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ControlledRefreshSignalClient : IRefreshSignalClient
    {
        private Func<CancellationToken, Task>? _onRefreshRequested;
        private Action<bool, bool, string?>? _onConnectionStateChanged;

        public Task StartAsync(
            Func<CancellationToken, Task> onRefreshRequested,
            Action<bool, bool, string?> onConnectionStateChanged,
            CancellationToken cancellationToken = default)
        {
            _onRefreshRequested = onRefreshRequested;
            _onConnectionStateChanged = onConnectionStateChanged;
            _onConnectionStateChanged(true, false, null);
            return Task.CompletedTask;
        }

        public void TriggerRefresh()
        {
            Assert.IsNotNull(_onRefreshRequested);
            _onRefreshRequested(CancellationToken.None).GetAwaiter().GetResult();
        }

        public void TriggerReconnecting()
        {
            Assert.IsNotNull(_onConnectionStateChanged);
            _onConnectionStateChanged(false, true, "Live updates reconnecting...");
        }

        public void TriggerReconnectRecovered()
        {
            Assert.IsNotNull(_onConnectionStateChanged);
            Assert.IsNotNull(_onRefreshRequested);
            _onConnectionStateChanged(true, false, null);
            _onRefreshRequested(CancellationToken.None).GetAwaiter().GetResult();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubPollingFallback : IPollingFallback
    {
        public bool IsActive { get; private set; }

        public void Start(Func<CancellationToken, Task> onRefreshRequested, CancellationToken cancellationToken)
        {
            IsActive = true;
        }

        public void Stop()
        {
            IsActive = false;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SidebarSnapshotMessageHandler(params ProjectSidebarSnapshotDto[] snapshots) : HttpMessageHandler
    {
        private readonly Queue<ProjectSidebarSnapshotDto> _snapshots = new(snapshots);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.IsTrue(_snapshots.Count > 0, $"Unexpected request: {request.Method} {request.RequestUri}");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(_snapshots.Dequeue()),
            });
        }
    }
}
