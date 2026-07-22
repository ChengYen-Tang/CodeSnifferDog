using System.Security.Claims;
using CodeSnifferDog.Server.Hubs;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;

namespace CodeSnifferDog.Tests.Hubs;

[TestClass]
public sealed class ProjectUpdatesHubTests
{
    private static readonly DateTimeOffset SnapshotGeneratedAtUtc =
        new(2026, 7, 21, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task SubscribeToProject_WithAgent_AddsGroupsBeforeBackfillAndReturnsOneArray()
    {
        Guid projectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid agentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        List<string> events = [];
        LiveUpdateDto[] expectedUpdates =
        [
            CreateUpdate(projectId, LiveUpdateKind.AgentGroupUpserted, minute: 1),
            CreateUpdate(projectId, LiveUpdateKind.AgentUpserted, minute: 2),
        ];
        RecordingGroupManager groups = new(events);
        RecordingLiveBackfillService backfill = new(events)
        {
            Updates = expectedUpdates,
        };
        ProjectUpdatesHub hub = CreateHub(backfill, groups);
        LiveSubscriptionRequestDto request = CreateRequest(projectId, agentId);

        LiveUpdateDto[] result = await hub.SubscribeToProject(request);

        Assert.HasCount(3, events);
        Assert.AreEqual($"add:{ProjectUpdatesContract.GetProjectChannelName(projectId)}", events[0]);
        Assert.AreEqual($"add:{ProjectUpdatesContract.GetProjectAgentChannelName(projectId, agentId)}", events[1]);
        Assert.AreEqual("backfill", events[2]);
        Assert.HasCount(2, result);
        Assert.AreSame(expectedUpdates[0], result[0]);
        Assert.AreSame(expectedUpdates[1], result[1]);
        Assert.AreNotSame(expectedUpdates, result);
        Assert.AreSame(request, Assert.ContainsSingle(backfill.Requests));
    }

    [TestMethod]
    public async Task SubscribeToProject_WhenConnectionAbortedAndBackfillCancels_ReturnsEmptyArray()
    {
        Guid projectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Guid agentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        using CancellationTokenSource connectionAborted = new();
        connectionAborted.Cancel();
        List<string> events = [];
        RecordingGroupManager groups = new(events);
        RecordingLiveBackfillService backfill = new(events)
        {
            ExceptionFactory = cancellationToken => new OperationCanceledException(cancellationToken),
        };
        ProjectUpdatesHub hub = CreateHub(backfill, groups, connectionAborted.Token);

        LiveUpdateDto[] result = await hub.SubscribeToProject(CreateRequest(projectId, agentId));

        Assert.IsEmpty(result);
        Assert.IsTrue(Assert.ContainsSingle(backfill.CancellationTokens).IsCancellationRequested);
        Assert.AreEqual("backfill", events[^1]);
    }

    [TestMethod]
    public async Task SubscribeToProject_WhenAgentChanges_PreservesProjectGroupAndSwapsAgentGroup()
    {
        const string connectionId = "connection-1";
        Guid projectId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        Guid agentAId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        Guid agentBId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
        RecordingGroupManager groups = new([]);
        RecordingLiveBackfillService backfill = new([]);
        ProjectUpdatesHub hub = CreateHub(backfill, groups, connectionId: connectionId);
        string projectGroup = ProjectUpdatesContract.GetProjectChannelName(projectId);
        string agentAGroup = ProjectUpdatesContract.GetProjectAgentChannelName(projectId, agentAId);
        string agentBGroup = ProjectUpdatesContract.GetProjectAgentChannelName(projectId, agentBId);

        await hub.SubscribeToProject(CreateRequest(projectId, agentAId));
        await hub.SubscribeToProject(CreateRequest(projectId, agentBId));

        Assert.Contains((connectionId, projectGroup), groups.Memberships);
        Assert.DoesNotContain((connectionId, agentAGroup), groups.Memberships);
        Assert.Contains((connectionId, agentBGroup), groups.Memberships);
        Assert.IsFalse(groups.Operations.Any(operation =>
            operation.Kind == "remove" && operation.GroupName == projectGroup));
        GroupOperation removedAgent = Assert.ContainsSingle(groups.Operations.Where(operation =>
            operation.Kind == "remove" && operation.GroupName == agentAGroup));
        Assert.AreEqual(connectionId, removedAgent.ConnectionId);
        GroupOperation addedAgent = Assert.ContainsSingle(groups.Operations.Where(operation =>
            operation.Kind == "add" && operation.GroupName == agentBGroup));
        Assert.AreEqual(connectionId, addedAgent.ConnectionId);
    }

    private static ProjectUpdatesHub CreateHub(
        ILiveBackfillService backfill,
        IGroupManager groups,
        CancellationToken connectionAborted = default,
        string connectionId = "connection-1")
    {
        return new ProjectUpdatesHub(backfill)
        {
            Context = new TestHubCallerContext(connectionId, connectionAborted),
            Groups = groups,
        };
    }

    private static LiveSubscriptionRequestDto CreateRequest(Guid projectId, Guid? agentId)
    {
        return new LiveSubscriptionRequestDto
        {
            ProjectId = projectId,
            SnapshotGeneratedAtUtc = SnapshotGeneratedAtUtc,
            AgentId = agentId,
            LatestSequence = 42,
        };
    }

    private static LiveUpdateDto CreateUpdate(Guid projectId, LiveUpdateKind kind, int minute)
    {
        return new LiveUpdateDto
        {
            ProjectId = projectId,
            Kind = kind,
            OccurredAtUtc = SnapshotGeneratedAtUtc.AddMinutes(minute),
        };
    }

    private sealed class RecordingLiveBackfillService(List<string> events) : ILiveBackfillService
    {
        public IReadOnlyList<LiveUpdateDto> Updates { get; init; } = [];

        public Func<CancellationToken, Exception>? ExceptionFactory { get; init; }

        public List<LiveSubscriptionRequestDto> Requests { get; } = [];

        public List<CancellationToken> CancellationTokens { get; } = [];

        public Task<IReadOnlyList<LiveUpdateDto>> GetBackfillAsync(
            LiveSubscriptionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            events.Add("backfill");
            Requests.Add(request);
            CancellationTokens.Add(cancellationToken);

            if (ExceptionFactory is not null)
            {
                return Task.FromException<IReadOnlyList<LiveUpdateDto>>(ExceptionFactory(cancellationToken));
            }

            return Task.FromResult(Updates);
        }
    }

    private sealed class RecordingGroupManager(List<string> events) : IGroupManager
    {
        public HashSet<(string ConnectionId, string GroupName)> Memberships { get; } = [];

        public List<GroupOperation> Operations { get; } = [];

        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            events.Add($"add:{groupName}");
            Operations.Add(new GroupOperation("add", connectionId, groupName));
            Memberships.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            events.Add($"remove:{groupName}");
            Operations.Add(new GroupOperation("remove", connectionId, groupName));
            Memberships.Remove((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private sealed record GroupOperation(string Kind, string ConnectionId, string GroupName);

    private sealed class TestHubCallerContext(
        string connectionId,
        CancellationToken connectionAborted) : HubCallerContext
    {
        public override string ConnectionId { get; } = connectionId;

        public override string? UserIdentifier => null;

        public override ClaimsPrincipal? User => null;

        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted { get; } = connectionAborted;

        public override void Abort()
        {
        }
    }
}
