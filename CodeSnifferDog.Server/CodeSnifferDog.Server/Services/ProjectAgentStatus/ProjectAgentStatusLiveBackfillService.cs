using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectAgentStatus;

public sealed class ProjectAgentStatusLiveBackfillService(IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory)
    : IProjectAgentStatusLiveBackfillService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;

    public async Task<IReadOnlyList<ProjectAgentLiveUpdateDto>> GetBackfillAsync(
        ProjectAgentLiveSubscriptionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<ProjectAgentGroupRecord> groups = await dbContext.ProjectAgentGroups
            .AsNoTracking()
            .Where(group => group.ProjectId == request.ProjectId)
            .OrderBy(group => group.CreatedAtUtc)
            .ThenBy(group => group.DisplayName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<Guid> groupIds = groups.Select(group => group.Id).ToList();
        List<ProjectAgentRecord> agents = await dbContext.ProjectAgents
            .AsNoTracking()
            .Where(agent => groupIds.Contains(agent.ProjectAgentGroupId))
            .OrderBy(agent => agent.CreatedAtUtc)
            .ThenBy(agent => agent.DisplayName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, long> cursorByAgentId = request.AgentCursors.ToDictionary(
            cursor => cursor.AgentId,
            cursor => cursor.LatestSequence);

        List<Guid> agentIds = agents.Select(agent => agent.Id).ToList();
        List<ProjectAgentTimelineEntryRecord> timelineEntries = await dbContext.ProjectAgentTimelineEntries
            .AsNoTracking()
            .Where(entry => agentIds.Contains(entry.ProjectAgentId))
            .OrderBy(entry => entry.ProjectAgentId)
            .ThenBy(entry => entry.Sequence)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ProjectAgentLiveUpdateDto> updates = [];
        updates.AddRange(groups.Select(group => new ProjectAgentLiveUpdateDto
        {
            ProjectId = request.ProjectId,
            Kind = ProjectAgentLiveUpdateKind.AgentGroupUpserted,
            OccurredAtUtc = group.CreatedAtUtc,
            Group = new ProjectAgentGroupLiveDto
            {
                GroupId = group.Id,
                RuntimeKey = group.RuntimeKey,
                DisplayName = group.DisplayName,
                CreatedAtUtc = group.CreatedAtUtc,
            },
        }));

        updates.AddRange(agents.Select(agent => new ProjectAgentLiveUpdateDto
        {
            ProjectId = request.ProjectId,
            Kind = ProjectAgentLiveUpdateKind.AgentUpserted,
            OccurredAtUtc = agent.CreatedAtUtc,
            Agent = new ProjectAgentLiveDto
            {
                AgentId = agent.Id,
                GroupId = agent.ProjectAgentGroupId,
                RuntimeKey = agent.RuntimeKey,
                DisplayName = agent.DisplayName,
                Status = MapAgentStatus(agent.Status),
                CreatedAtUtc = agent.CreatedAtUtc,
            },
        }));

        updates.AddRange(timelineEntries
            .Where(entry => entry.Sequence > cursorByAgentId.GetValueOrDefault(entry.ProjectAgentId, 0))
            .Select(entry => new ProjectAgentLiveUpdateDto
            {
                ProjectId = request.ProjectId,
                Kind = ProjectAgentLiveUpdateKind.TimelineEntryUpserted,
                OccurredAtUtc = entry.OccurredAtUtc,
                TimelineEntry = new ProjectAgentTimelineEntryDto
                {
                    TimelineEntryId = entry.Id,
                    AgentId = entry.ProjectAgentId,
                    Sequence = entry.Sequence,
                    EntryKind = MapTimelineEntryKind(entry.EntryType),
                    OccurredAtUtc = entry.OccurredAtUtc,
                    Message = entry.Message,
                    ToolCallId = entry.ToolCallId,
                    ToolName = entry.ToolName,
                    ToolArguments = entry.ToolArguments,
                    ToolResult = entry.ToolResult,
                },
            }));

        return updates;
    }

    private static ProjectAgentRunStatus MapAgentStatus(Data.Entities.ProjectAgentStatus status) => status switch
    {
        Data.Entities.ProjectAgentStatus.Waiting => ProjectAgentRunStatus.Waiting,
        Data.Entities.ProjectAgentStatus.Running => ProjectAgentRunStatus.Running,
        Data.Entities.ProjectAgentStatus.Completed => ProjectAgentRunStatus.Completed,
        Data.Entities.ProjectAgentStatus.Degraded => ProjectAgentRunStatus.Degraded,
        _ => throw new InvalidOperationException($"Unsupported agent status '{status}'."),
    };

    private static ProjectAgentTimelineEntryKind MapTimelineEntryKind(ProjectAgentTimelineEntryType entryType) => entryType switch
    {
        ProjectAgentTimelineEntryType.Input => ProjectAgentTimelineEntryKind.Input,
        ProjectAgentTimelineEntryType.Output => ProjectAgentTimelineEntryKind.Output,
        ProjectAgentTimelineEntryType.Tool => ProjectAgentTimelineEntryKind.Tool,
        ProjectAgentTimelineEntryType.Compaction => ProjectAgentTimelineEntryKind.Compaction,
        _ => throw new InvalidOperationException($"Unsupported timeline entry type '{entryType}'."),
    };
}
