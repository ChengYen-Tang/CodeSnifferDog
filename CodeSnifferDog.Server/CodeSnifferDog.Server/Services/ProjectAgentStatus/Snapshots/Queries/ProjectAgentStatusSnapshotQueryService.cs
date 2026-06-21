using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

internal sealed class ProjectAgentStatusSnapshotQueryService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory)
    : IProjectAgentStatusSnapshotQueryService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;

    public async Task<ProjectAgentStatusSnapshotReadModel?> GetSnapshotAsync(
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

        List<AgentStatusGroupProjection> groups = await dbContext.ProjectAgentGroups
            .AsNoTracking()
            .Where(group => group.ProjectId == projectId)
            .OrderBy(group => group.CreatedAtUtc)
            .ThenBy(group => group.DisplayName)
            .Select(group => new AgentStatusGroupProjection(
                group.Id,
                group.RuntimeKey,
                group.DisplayName,
                group.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Guid> groupIds = groups.Select(group => group.GroupId).ToList();

        List<AgentStatusAgentProjection> agents = await dbContext.ProjectAgents
            .AsNoTracking()
            .Where(agent => groupIds.Contains(agent.ProjectAgentGroupId))
            .OrderBy(agent => agent.CreatedAtUtc)
            .ThenBy(agent => agent.DisplayName)
            .Select(agent => new AgentStatusAgentProjection(
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
        IReadOnlyList<AgentStatusTimelineEntryProjection> selectedTimelineEntries =
            effectiveSelectedAgentId is Guid selectedHistoryAgentId
                ? await LoadTimelineEntriesAsync(dbContext, selectedHistoryAgentId, cancellationToken).ConfigureAwait(false)
                : [];

        IReadOnlyList<ProjectAgentStatusSnapshotGroupRow> groupRows = groups
            .OrderBy(group => group.CreatedAtUtc)
            .ThenBy(group => group.DisplayName, StringComparer.Ordinal)
            .Select(group => new ProjectAgentStatusSnapshotGroupRow(
                group,
                agents
                    .Where(agent => agent.GroupId == group.GroupId)
                    .OrderBy(agent => agent.CreatedAtUtc)
                    .ThenBy(agent => agent.DisplayName, StringComparer.Ordinal)
                    .Select(agent => new ProjectAgentStatusSnapshotAgentRow(
                        agent,
                        HasLoadedHistory: effectiveSelectedAgentId == agent.AgentId,
                        TimelineEntries: effectiveSelectedAgentId == agent.AgentId ? selectedTimelineEntries : []))
                    .ToList()))
            .ToList();

        return new ProjectAgentStatusSnapshotReadModel(
            project.ProjectId,
            project.Status,
            groupRows);
    }

    public async Task<ProjectAgentHistorySnapshotReadModel?> GetAgentHistoryAsync(
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

        IReadOnlyList<AgentStatusTimelineEntryProjection> timelineEntries =
            await LoadTimelineEntriesAsync(dbContext, agentId, cancellationToken).ConfigureAwait(false);

        return new ProjectAgentHistorySnapshotReadModel(
            projectId,
            agentId,
            timelineEntries);
    }

    private static Guid? ResolveSelectedAgentId(
        IReadOnlyList<AgentStatusAgentProjection> agents,
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

    private static Task<List<AgentStatusTimelineEntryProjection>> LoadTimelineEntriesAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        CancellationToken cancellationToken) =>
        dbContext.ProjectAgentTimelineEntries
            .AsNoTracking()
            .Where(entry => entry.ProjectAgentId == agentId)
            .OrderBy(entry => entry.Sequence)
            .Select(entry => new AgentStatusTimelineEntryProjection(
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
