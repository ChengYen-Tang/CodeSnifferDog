using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectAgentSnapshots;

public sealed class ProjectAgentStatusSnapshotService(IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory)
    : IProjectAgentStatusSnapshotService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;

    public async Task<ProjectAgentStatusSnapshotDto?> GetSnapshotAsync(
        Guid projectId,
        Guid? selectedAgentId = null,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectSnapshotRow? project = await dbContext.Projects
            .AsNoTracking()
            .Where(candidate => candidate.Id == projectId)
            .Select(candidate => new ProjectSnapshotRow(
                candidate.Id,
                candidate.Status))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (project is null)
            return null;

        List<GroupSnapshotRow> groups = await dbContext.ProjectAgentGroups
            .AsNoTracking()
            .Where(group => group.ProjectId == projectId)
            .OrderBy(group => group.CreatedAtUtc)
            .ThenBy(group => group.DisplayName)
            .Select(group => new GroupSnapshotRow(
                group.Id,
                group.RuntimeKey,
                group.DisplayName,
                group.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Guid> groupIds = groups.Select(group => group.GroupId).ToList();

        List<AgentSnapshotRow> agents = await dbContext.ProjectAgents
            .AsNoTracking()
            .Where(agent => groupIds.Contains(agent.ProjectAgentGroupId))
            .OrderBy(agent => agent.CreatedAtUtc)
            .ThenBy(agent => agent.DisplayName)
            .Select(agent => new AgentSnapshotRow(
                agent.Id,
                agent.ProjectAgentGroupId,
                agent.RuntimeKey,
                agent.DisplayName,
                agent.Status,
                agent.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid? effectiveSelectedAgentId = ResolveSelectedAgentId(agents, selectedAgentId);
        List<TimelineEntrySnapshotRow> timelineEntries =
            effectiveSelectedAgentId is Guid selectedHistoryAgentId
                ? await LoadTimelineEntriesAsync(dbContext, selectedHistoryAgentId, cancellationToken).ConfigureAwait(false)
                : [];

        IReadOnlyList<ProjectAgentTimelineEntryDto> selectedTimelineEntries = timelineEntries
            .OrderBy(entry => entry.Sequence)
            .Select(MapTimelineEntry)
            .ToList();

        Dictionary<Guid, IReadOnlyList<ProjectAgentTimelineEntryDto>> timelineByAgentId =
            effectiveSelectedAgentId is Guid selectedAgentWithHistory
                ? new Dictionary<Guid, IReadOnlyList<ProjectAgentTimelineEntryDto>>
                {
                    [selectedAgentWithHistory] = selectedTimelineEntries,
                }
                : [];

        Dictionary<Guid, IReadOnlyList<ProjectAgentSnapshotDto>> agentsByGroupId = agents
            .GroupBy(agent => agent.GroupId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProjectAgentSnapshotDto>)group
                    .OrderBy(agent => agent.CreatedAtUtc)
                    .ThenBy(agent => agent.DisplayName, StringComparer.Ordinal)
                    .Select(agent => MapAgent(agent, timelineByAgentId, effectiveSelectedAgentId))
                    .ToList());

        return new ProjectAgentStatusSnapshotDto
        {
            ProjectId = project.ProjectId,
            ProjectStatus = MapProjectStatus(project.Status),
            SnapshotGeneratedAtUtc = DateTimeOffset.UtcNow,
            AgentGroups = groups
                .OrderBy(group => group.CreatedAtUtc)
                .ThenBy(group => group.DisplayName, StringComparer.Ordinal)
                .Select(group => MapGroup(group, agentsByGroupId))
                .ToList(),
        };
    }

    public async Task<ProjectAgentHistorySnapshotDto?> GetAgentHistoryAsync(
        Guid projectId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        bool agentExists = await dbContext.ProjectAgents
            .AsNoTracking()
            .Where(agent => agent.Id == agentId)
            .Join(
                dbContext.ProjectAgentGroups.AsNoTracking(),
                agent => agent.ProjectAgentGroupId,
                group => group.Id,
                (agent, group) => new { agent.Id, group.ProjectId })
            .AnyAsync(item => item.Id == agentId && item.ProjectId == projectId, cancellationToken)
            .ConfigureAwait(false);

        if (!agentExists)
            return null;

        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries = (await LoadTimelineEntriesAsync(
                dbContext,
                agentId,
                cancellationToken)
            .ConfigureAwait(false))
            .OrderBy(entry => entry.Sequence)
            .Select(MapTimelineEntry)
            .ToList();

        return new ProjectAgentHistorySnapshotDto
        {
            ProjectId = projectId,
            AgentId = agentId,
            TimelineEntries = timelineEntries,
        };
    }

    private static ProjectAgentGroupSnapshotDto MapGroup(
        GroupSnapshotRow group,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProjectAgentSnapshotDto>> agentsByGroupId) => new()
    {
        GroupId = group.GroupId,
        RuntimeKey = group.RuntimeKey,
        DisplayName = group.DisplayName,
        CreatedAtUtc = group.CreatedAtUtc,
        Agents = agentsByGroupId.TryGetValue(group.GroupId, out IReadOnlyList<ProjectAgentSnapshotDto>? agents)
            ? agents
            : [],
    };

    private static ProjectAgentSnapshotDto MapAgent(
        AgentSnapshotRow agent,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProjectAgentTimelineEntryDto>> timelineByAgentId,
        Guid? selectedAgentId) => new()
    {
        AgentId = agent.AgentId,
        GroupId = agent.GroupId,
        RuntimeKey = agent.RuntimeKey,
        DisplayName = agent.DisplayName,
        Status = MapAgentStatus(agent.Status),
        CreatedAtUtc = agent.CreatedAtUtc,
        HasLoadedHistory = selectedAgentId == agent.AgentId,
        TimelineEntries = timelineByAgentId.TryGetValue(agent.AgentId, out IReadOnlyList<ProjectAgentTimelineEntryDto>? timeline)
            ? timeline
            : [],
    };

    private static Guid? ResolveSelectedAgentId(
        IReadOnlyList<AgentSnapshotRow> agents,
        Guid? selectedAgentId)
    {
        if (selectedAgentId is Guid requestedAgentId && agents.Any(agent => agent.AgentId == requestedAgentId))
            return requestedAgentId;

        return agents
            .OrderBy(agent => agent.CreatedAtUtc)
            .ThenBy(agent => agent.DisplayName, StringComparer.Ordinal)
            .Select(agent => (Guid?)agent.AgentId)
            .FirstOrDefault();
    }

    private static Task<List<TimelineEntrySnapshotRow>> LoadTimelineEntriesAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        CancellationToken cancellationToken) =>
        dbContext.ProjectAgentTimelineEntries
            .AsNoTracking()
            .Where(entry => entry.ProjectAgentId == agentId)
            .OrderBy(entry => entry.Sequence)
            .Select(entry => new TimelineEntrySnapshotRow(
                entry.Id,
                entry.ProjectAgentId,
                entry.Sequence,
                entry.EntryType,
                entry.OccurredAtUtc,
                entry.Message,
                entry.ToolCallId,
                entry.ToolName,
                entry.ToolArguments,
                entry.ToolResult))
            .ToListAsync(cancellationToken);

    private static ProjectAgentTimelineEntryDto MapTimelineEntry(TimelineEntrySnapshotRow entry) => new()
    {
        TimelineEntryId = entry.TimelineEntryId,
        AgentId = entry.AgentId,
        Sequence = entry.Sequence,
        EntryKind = MapEntryKind(entry.EntryType),
        OccurredAtUtc = entry.OccurredAtUtc,
        Message = entry.Message,
        ToolCallId = entry.ToolCallId,
        ToolName = entry.ToolName,
        ToolArguments = entry.ToolArguments,
        ToolResult = entry.ToolResult,
    };

    private static ProjectStatus MapProjectStatus(ProjectProcessingStatus status) => status switch
    {
        ProjectProcessingStatus.Queued => ProjectStatus.Queued,
        ProjectProcessingStatus.Reviewing => ProjectStatus.Reviewing,
        ProjectProcessingStatus.Completed => ProjectStatus.Completed,
        ProjectProcessingStatus.Failed => ProjectStatus.Failed,
        ProjectProcessingStatus.Canceled => ProjectStatus.Canceled,
        _ => throw new InvalidOperationException($"Unsupported project status '{status}'."),
    };

    private static ProjectAgentRunStatus MapAgentStatus(Data.Entities.ProjectAgentStatus status) => status switch
    {
        Data.Entities.ProjectAgentStatus.Waiting => ProjectAgentRunStatus.Waiting,
        Data.Entities.ProjectAgentStatus.Running => ProjectAgentRunStatus.Running,
        Data.Entities.ProjectAgentStatus.Completed => ProjectAgentRunStatus.Completed,
        Data.Entities.ProjectAgentStatus.Degraded => ProjectAgentRunStatus.Degraded,
        _ => throw new InvalidOperationException($"Unsupported agent status '{status}'."),
    };

    private static ProjectAgentTimelineEntryKind MapEntryKind(ProjectAgentTimelineEntryType entryType) => entryType switch
    {
        ProjectAgentTimelineEntryType.Input => ProjectAgentTimelineEntryKind.Input,
        ProjectAgentTimelineEntryType.Output => ProjectAgentTimelineEntryKind.Output,
        ProjectAgentTimelineEntryType.Tool => ProjectAgentTimelineEntryKind.Tool,
        ProjectAgentTimelineEntryType.Compaction => ProjectAgentTimelineEntryKind.Compaction,
        _ => throw new InvalidOperationException($"Unsupported timeline entry type '{entryType}'."),
    };

    private sealed record ProjectSnapshotRow(Guid ProjectId, ProjectProcessingStatus Status);

    private sealed record GroupSnapshotRow(Guid GroupId, string RuntimeKey, string DisplayName, DateTimeOffset CreatedAtUtc);

    private sealed record AgentSnapshotRow(
        Guid AgentId,
        Guid GroupId,
        string RuntimeKey,
        string DisplayName,
        Data.Entities.ProjectAgentStatus Status,
        DateTimeOffset CreatedAtUtc);

    private sealed record TimelineEntrySnapshotRow(
        Guid TimelineEntryId,
        Guid AgentId,
        long Sequence,
        ProjectAgentTimelineEntryType EntryType,
        DateTimeOffset OccurredAtUtc,
        string? Message,
        string? ToolCallId,
        string? ToolName,
        string? ToolArguments,
        string? ToolResult);
}
