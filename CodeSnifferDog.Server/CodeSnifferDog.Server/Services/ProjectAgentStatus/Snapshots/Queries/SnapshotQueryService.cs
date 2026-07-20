using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;

/// <summary>
/// Loads persisted rows required to build status and history snapshots for one project.
/// </summary>
/// <param name="dbContextFactory">Factory used to create database contexts for read queries.</param>
internal sealed class SnapshotQueryService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory)
    : ISnapshotQueryService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;

    /// <inheritdoc />
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
                null,
                agent.Status,
                agent.CreatedAtUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid? effectiveSelectedAgentId = ResolveSelectedAgentId(agents, selectedAgentId);
        string? selectedSystemPrompt =
            effectiveSelectedAgentId is Guid selectedPromptAgentId
                ? await LoadSystemPromptAsync(dbContext, selectedPromptAgentId, cancellationToken).ConfigureAwait(false)
                : null;
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
                        SystemPrompt: effectiveSelectedAgentId == agent.AgentId ? selectedSystemPrompt : null,
                        HasLoadedHistory: effectiveSelectedAgentId == agent.AgentId,
                        TimelineEntries: effectiveSelectedAgentId == agent.AgentId ? selectedTimelineEntries : []))
                    .ToList()))
            .ToList();

        return new SnapshotReadModel(
            project.ProjectId,
            project.Status,
            groupRows);
    }

    /// <inheritdoc />
    public async Task<HistorySnapshotReadModel?> GetAgentHistoryAsync(
        Guid projectId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var agentHeader = await dbContext.ProjectAgents
            .AsNoTracking()
            .Where(agent => agent.Id == agentId)
            .Join(
                dbContext.ProjectAgentGroups
                    .AsNoTracking()
                    .Where(group => group.ProjectId == projectId),
                agent => agent.ProjectAgentGroupId,
                group => group.Id,
                (agent, group) => new
                {
                    AgentId = agent.Id,
                    group.ProjectId,
                    agent.SystemPrompt,
                })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (agentHeader is null)
            return null;

        IReadOnlyList<TimelineEntryProjection> timelineEntries =
            await LoadTimelineEntriesAsync(dbContext, agentId, cancellationToken).ConfigureAwait(false);

        return new HistorySnapshotReadModel(
            projectId,
            agentId,
            agentHeader.SystemPrompt,
            timelineEntries);
    }

    /// <summary>
    /// Resolves the selected agent identifier against the loaded agents.
    /// </summary>
    /// <param name="agents">Loaded agent projections.</param>
    /// <param name="selectedAgentId">Requested selected agent identifier.</param>
    /// <returns>The requested identifier when present; otherwise the first available agent identifier.</returns>
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

    /// <summary>
    /// Loads all timeline entries for one agent ordered by sequence.
    /// </summary>
    /// <param name="dbContext">Database context used for the query.</param>
    /// <param name="agentId">Agent identifier whose timeline should be loaded.</param>
    /// <param name="cancellationToken">Cancels query execution.</param>
    /// <returns>The ordered timeline-entry projections.</returns>
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

    /// <summary>
    /// Loads the prompt for the one agent whose details are included in a snapshot.
    /// </summary>
    private static Task<string> LoadSystemPromptAsync(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        CancellationToken cancellationToken) =>
        dbContext.ProjectAgents
            .AsNoTracking()
            .Where(agent => agent.Id == agentId)
            .Select(agent => agent.SystemPrompt)
            .SingleAsync(cancellationToken);

    /// <summary>
    /// Lightweight project row used when building status snapshots.
    /// </summary>
    /// <param name="ProjectId">Project identifier.</param>
    /// <param name="Status">Persisted project status.</param>
    private sealed record ProjectSnapshotRow(Guid ProjectId, ProjectProcessingStatus Status);

}
