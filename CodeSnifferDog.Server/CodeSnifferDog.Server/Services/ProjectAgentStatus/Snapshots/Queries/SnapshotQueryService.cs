using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

internal sealed class SnapshotQueryService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory)
    : ISnapshotQueryService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;

    public async Task<SnapshotReadModel?> GetSnapshotAsync(
        Guid projectId,
        Guid? selectedAgentId = null,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

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

        List<GroupProjection> groups = await dbContext.ProjectAgentGroups
            .AsNoTracking()
            .Where(group => group.ProjectId == projectId)
            .OrderBy(group => group.CreatedAtUtc)
            .ThenBy(group => group.DisplayName)
            .Select(group => new GroupProjection(
                group.Id,
                group.RuntimeKey,
                group.DisplayName,
                group.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Guid> groupIds = groups.Select(group => group.GroupId).ToList();

        List<AgentProjection> agents = await dbContext.ProjectAgents
            .AsNoTracking()
            .Where(agent => groupIds.Contains(agent.ProjectAgentGroupId))
            .OrderBy(agent => agent.CreatedAtUtc)
            .ThenBy(agent => agent.DisplayName)
            .Select(agent => new AgentProjection(
                agent.Id,
                agent.ProjectAgentGroupId,
                agent.RuntimeKey,
                agent.DisplayName,
                agent.SystemPrompt,
                agent.Status,
                agent.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid? effectiveSelectedAgentId = ResolveSelectedAgentId(agents, selectedAgentId);
        IReadOnlyList<TimelineEntryProjection> selectedTimelineEntries =
            effectiveSelectedAgentId is Guid selectedHistoryAgentId
                ? await LoadTimelineEntriesAsync(dbContext, selectedHistoryAgentId, cancellationToken).ConfigureAwait(false)
                : [];

        IReadOnlyList<SnapshotGroupRow> groupRows = groups
            .OrderBy(group => group.CreatedAtUtc)
            .ThenBy(group => group.DisplayName, StringComparer.Ordinal)
            .Select(group => new SnapshotGroupRow(
                group,
                agents
                    .Where(agent => agent.GroupId == group.GroupId)
                    .OrderBy(agent => agent.CreatedAtUtc)
                    .ThenBy(agent => agent.DisplayName, StringComparer.Ordinal)
                    .Select(agent => new SnapshotAgentRow(
                        agent,
                        HasLoadedHistory: effectiveSelectedAgentId == agent.AgentId,
                        TimelineEntries: effectiveSelectedAgentId == agent.AgentId ? selectedTimelineEntries : []))
                    .ToList()))
            .ToList();

        return new SnapshotReadModel(
            project.ProjectId,
            project.Status,
            groupRows);
    }

    public async Task<HistorySnapshotReadModel?> GetAgentHistoryAsync(
        Guid projectId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

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

        IReadOnlyList<TimelineEntryProjection> timelineEntries =
            await LoadTimelineEntriesAsync(dbContext, agentId, cancellationToken).ConfigureAwait(false);

        return new HistorySnapshotReadModel(
            projectId,
            agentId,
            timelineEntries);
    }

    private static Guid? ResolveSelectedAgentId(
        IReadOnlyList<AgentProjection> agents,
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

    private static Task<List<TimelineEntryProjection>> LoadTimelineEntriesAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        CancellationToken cancellationToken) =>
        dbContext.ProjectAgentTimelineEntries
            .AsNoTracking()
            .Where(entry => entry.ProjectAgentId == agentId)
            .OrderBy(entry => entry.Sequence)
            .Select(entry => new TimelineEntryProjection(
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

    private sealed record ProjectSnapshotRow(Guid ProjectId, ProjectProcessingStatus Status);
}
