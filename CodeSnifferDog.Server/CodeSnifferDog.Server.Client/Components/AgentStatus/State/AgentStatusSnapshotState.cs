using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed class AgentStatusSnapshotState(ProjectAgentStatusSnapshotDto? snapshot)
{
    public ProjectAgentStatusSnapshotDto? Snapshot { get; private set; } = snapshot;

    public IReadOnlyList<ProjectAgentGroupSnapshotDto> Groups => Snapshot?.AgentGroups ?? [];

    public void Replace(ProjectAgentStatusSnapshotDto? snapshot)
    {
        Snapshot = snapshot;
    }

    public bool ApplyLiveUpdate(ProjectAgentLiveUpdateDto update)
    {
        if (Snapshot is null)
            return false;

        return update.Kind switch
        {
            ProjectAgentLiveUpdateKind.ProjectStatusChanged => UpdateProjectStatus(update.ProjectStatus),
            ProjectAgentLiveUpdateKind.AgentGroupUpserted => UpsertGroup(update.Group),
            ProjectAgentLiveUpdateKind.AgentUpserted => UpsertAgent(update.Agent),
            ProjectAgentLiveUpdateKind.AgentStatusChanged => UpdateAgentStatus(update.AgentStatus),
            _ => false,
        };
    }

    public ProjectAgentSnapshotDto? FindAgent(Guid agentId) =>
        Groups.SelectMany(group => group.Agents).FirstOrDefault(agent => agent.AgentId == agentId);

    public IReadOnlyList<ProjectAgentTimelineEntryDto> GetHistory(Guid agentId) =>
        FindAgent(agentId)?.TimelineEntries ?? [];

    public void ReleaseHistoryExcept(Guid? agentIdToKeep)
    {
        if (Snapshot is null)
            return;

        List<ProjectAgentGroupSnapshotDto> groups = Snapshot.AgentGroups
            .Select(group => new ProjectAgentGroupSnapshotDto
            {
                GroupId = group.GroupId,
                RuntimeKey = group.RuntimeKey,
                DisplayName = group.DisplayName,
                CreatedAtUtc = group.CreatedAtUtc,
                Agents = group.Agents
                    .Select(agent => new ProjectAgentSnapshotDto
                    {
                        AgentId = agent.AgentId,
                        GroupId = agent.GroupId,
                        RuntimeKey = agent.RuntimeKey,
                        DisplayName = agent.DisplayName,
                        SystemPrompt = agent.SystemPrompt,
                        Status = agent.Status,
                        CreatedAtUtc = agent.CreatedAtUtc,
                        HasLoadedHistory = agent.AgentId == agentIdToKeep && agent.HasLoadedHistory,
                        TimelineEntries = agent.AgentId == agentIdToKeep ? agent.TimelineEntries : [],
                    })
                    .ToList(),
            })
            .ToList();

        ReplaceSnapshotGroups(groups);
    }

    private bool UpsertGroup(ProjectAgentGroupLiveDto? group)
    {
        if (group is null || Snapshot is null)
            return false;

        List<ProjectAgentGroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        int existingIndex = groups.FindIndex(candidate => candidate.GroupId == group.GroupId);
        ProjectAgentGroupSnapshotDto nextGroup = existingIndex >= 0
            ? new ProjectAgentGroupSnapshotDto
            {
                GroupId = group.GroupId,
                RuntimeKey = group.RuntimeKey,
                DisplayName = group.DisplayName,
                CreatedAtUtc = group.CreatedAtUtc,
                Agents = groups[existingIndex].Agents,
            }
            : new ProjectAgentGroupSnapshotDto
            {
                GroupId = group.GroupId,
                RuntimeKey = group.RuntimeKey,
                DisplayName = group.DisplayName,
                CreatedAtUtc = group.CreatedAtUtc,
                Agents = [],
            };

        if (existingIndex >= 0)
            groups[existingIndex] = nextGroup;
        else
            groups.Add(nextGroup);

        ReplaceSnapshotGroups(groups);
        return true;
    }

    private bool UpsertAgent(ProjectAgentLiveDto? agent)
    {
        if (agent is null || Snapshot is null)
            return false;

        List<ProjectAgentGroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        int groupIndex = groups.FindIndex(group => group.GroupId == agent.GroupId);
        if (groupIndex < 0)
            return false;

        ProjectAgentGroupSnapshotDto group = groups[groupIndex];
        List<ProjectAgentSnapshotDto> agents = group.Agents.ToList();
        int existingIndex = agents.FindIndex(candidate => candidate.AgentId == agent.AgentId);
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
            existingIndex >= 0 ? agents[existingIndex].TimelineEntries : [];

        ProjectAgentSnapshotDto nextAgent = new()
        {
            AgentId = agent.AgentId,
            GroupId = agent.GroupId,
            RuntimeKey = agent.RuntimeKey,
            DisplayName = agent.DisplayName,
            Status = agent.Status,
            CreatedAtUtc = agent.CreatedAtUtc,
            SystemPrompt = agent.SystemPrompt,
            HasLoadedHistory = existingIndex >= 0 && agents[existingIndex].HasLoadedHistory,
            TimelineEntries = timelineEntries,
        };

        if (existingIndex >= 0)
            agents[existingIndex] = nextAgent;
        else
            agents.Add(nextAgent);

        groups[groupIndex] = new ProjectAgentGroupSnapshotDto
        {
            GroupId = group.GroupId,
            RuntimeKey = group.RuntimeKey,
            DisplayName = group.DisplayName,
            CreatedAtUtc = group.CreatedAtUtc,
            Agents = agents
                .OrderBy(candidate => candidate.CreatedAtUtc)
                .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal)
                .ToList(),
        };

        ReplaceSnapshotGroups(groups);
        return true;
    }

    private bool UpdateAgentStatus(ProjectAgentStatusChangedDto? agentStatus)
    {
        if (agentStatus is null || Snapshot is null)
            return false;

        List<ProjectAgentGroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            ProjectAgentGroupSnapshotDto group = groups[groupIndex];
            List<ProjectAgentSnapshotDto> agents = group.Agents.ToList();
            int agentIndex = agents.FindIndex(candidate => candidate.AgentId == agentStatus.AgentId);
            if (agentIndex < 0)
                continue;

            ProjectAgentSnapshotDto existingAgent = agents[agentIndex];
            agents[agentIndex] = new ProjectAgentSnapshotDto
            {
                AgentId = existingAgent.AgentId,
                GroupId = existingAgent.GroupId,
                RuntimeKey = existingAgent.RuntimeKey,
                DisplayName = existingAgent.DisplayName,
                SystemPrompt = existingAgent.SystemPrompt,
                Status = agentStatus.Status,
                CreatedAtUtc = existingAgent.CreatedAtUtc,
                HasLoadedHistory = existingAgent.HasLoadedHistory,
                TimelineEntries = existingAgent.TimelineEntries,
            };

            groups[groupIndex] = new ProjectAgentGroupSnapshotDto
            {
                GroupId = group.GroupId,
                RuntimeKey = group.RuntimeKey,
                DisplayName = group.DisplayName,
                CreatedAtUtc = group.CreatedAtUtc,
                Agents = agents,
            };

            ReplaceSnapshotGroups(groups);
            return true;
        }

        return false;
    }

    public bool UpsertTimelineEntry(ProjectAgentTimelineEntryDto? timelineEntry)
    {
        if (timelineEntry is null || Snapshot is null)
            return false;

        List<ProjectAgentGroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            ProjectAgentGroupSnapshotDto group = groups[groupIndex];
            List<ProjectAgentSnapshotDto> agents = group.Agents.ToList();
            int agentIndex = agents.FindIndex(candidate => candidate.AgentId == timelineEntry.AgentId);
            if (agentIndex < 0)
                continue;

            ProjectAgentSnapshotDto agent = agents[agentIndex];
            List<ProjectAgentTimelineEntryDto> timelineEntries = agent.TimelineEntries.ToList();
            int existingIndex = timelineEntries.FindIndex(candidate => candidate.TimelineEntryId == timelineEntry.TimelineEntryId);
            if (existingIndex >= 0)
                timelineEntries[existingIndex] = timelineEntry;
            else
                timelineEntries.Add(timelineEntry);

            agents[agentIndex] = new ProjectAgentSnapshotDto
            {
                AgentId = agent.AgentId,
                GroupId = agent.GroupId,
                RuntimeKey = agent.RuntimeKey,
                DisplayName = agent.DisplayName,
                SystemPrompt = agent.SystemPrompt,
                Status = agent.Status,
                CreatedAtUtc = agent.CreatedAtUtc,
                HasLoadedHistory = true,
                TimelineEntries = timelineEntries
                    .OrderBy(candidate => candidate.Sequence)
                    .ThenBy(candidate => candidate.OccurredAtUtc)
                    .ToList(),
            };

            groups[groupIndex] = new ProjectAgentGroupSnapshotDto
            {
                GroupId = group.GroupId,
                RuntimeKey = group.RuntimeKey,
                DisplayName = group.DisplayName,
                CreatedAtUtc = group.CreatedAtUtc,
                Agents = agents,
            };

            ReplaceSnapshotGroups(groups);
            return true;
        }

        return false;
    }

    public bool RemoveTimelineEntries(Guid agentId, IReadOnlyList<Guid> timelineEntryIds)
    {
        if (Snapshot is null || timelineEntryIds.Count == 0)
            return false;

        HashSet<Guid> timelineEntryIdSet = [.. timelineEntryIds];
        List<ProjectAgentGroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            ProjectAgentGroupSnapshotDto group = groups[groupIndex];
            List<ProjectAgentSnapshotDto> agents = group.Agents.ToList();
            int agentIndex = agents.FindIndex(candidate => candidate.AgentId == agentId);
            if (agentIndex < 0)
                continue;

            ProjectAgentSnapshotDto agent = agents[agentIndex];
            List<ProjectAgentTimelineEntryDto> timelineEntries = agent.TimelineEntries
                .Where(entry => !timelineEntryIdSet.Contains(entry.TimelineEntryId))
                .ToList();

            if (timelineEntries.Count == agent.TimelineEntries.Count)
                return false;

            agents[agentIndex] = new ProjectAgentSnapshotDto
            {
                AgentId = agent.AgentId,
                GroupId = agent.GroupId,
                RuntimeKey = agent.RuntimeKey,
                DisplayName = agent.DisplayName,
                SystemPrompt = agent.SystemPrompt,
                Status = agent.Status,
                CreatedAtUtc = agent.CreatedAtUtc,
                HasLoadedHistory = agent.HasLoadedHistory,
                TimelineEntries = timelineEntries,
            };

            groups[groupIndex] = new ProjectAgentGroupSnapshotDto
            {
                GroupId = group.GroupId,
                RuntimeKey = group.RuntimeKey,
                DisplayName = group.DisplayName,
                CreatedAtUtc = group.CreatedAtUtc,
                Agents = agents,
            };

            ReplaceSnapshotGroups(groups);
            return true;
        }

        return false;
    }

    public void ReplaceAgentHistory(Guid agentId, IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries)
    {
        if (Snapshot is null)
            return;

        List<ProjectAgentGroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            ProjectAgentGroupSnapshotDto group = groups[groupIndex];
            List<ProjectAgentSnapshotDto> agents = group.Agents.ToList();
            int agentIndex = agents.FindIndex(candidate => candidate.AgentId == agentId);
            if (agentIndex < 0)
                continue;

            ProjectAgentSnapshotDto existingAgent = agents[agentIndex];
            agents[agentIndex] = new ProjectAgentSnapshotDto
            {
                AgentId = existingAgent.AgentId,
                GroupId = existingAgent.GroupId,
                RuntimeKey = existingAgent.RuntimeKey,
                DisplayName = existingAgent.DisplayName,
                SystemPrompt = existingAgent.SystemPrompt,
                Status = existingAgent.Status,
                CreatedAtUtc = existingAgent.CreatedAtUtc,
                HasLoadedHistory = true,
                TimelineEntries = timelineEntries
                    .OrderBy(candidate => candidate.Sequence)
                    .ThenBy(candidate => candidate.OccurredAtUtc)
                    .ToList(),
            };

            groups[groupIndex] = new ProjectAgentGroupSnapshotDto
            {
                GroupId = group.GroupId,
                RuntimeKey = group.RuntimeKey,
                DisplayName = group.DisplayName,
                CreatedAtUtc = group.CreatedAtUtc,
                Agents = agents,
            };

            ReplaceSnapshotGroups(groups);
            return;
        }
    }

    private bool UpdateProjectStatus(ProjectExecutionStatusChangedDto? projectStatus)
    {
        if (projectStatus is null || Snapshot is null)
            return false;

        Snapshot = new ProjectAgentStatusSnapshotDto
        {
            ProjectId = Snapshot.ProjectId,
            ProjectStatus = projectStatus.Status,
            SnapshotGeneratedAtUtc = Snapshot.SnapshotGeneratedAtUtc,
            AgentGroups = Snapshot.AgentGroups,
        };

        return true;
    }

    private void ReplaceSnapshotGroups(IReadOnlyList<ProjectAgentGroupSnapshotDto> groups)
    {
        if (Snapshot is null)
            return;

        Snapshot = new ProjectAgentStatusSnapshotDto
        {
            ProjectId = Snapshot.ProjectId,
            ProjectStatus = Snapshot.ProjectStatus,
            SnapshotGeneratedAtUtc = Snapshot.SnapshotGeneratedAtUtc,
            AgentGroups = groups
                .OrderBy(group => group.CreatedAtUtc)
                .ThenBy(group => group.DisplayName, StringComparer.Ordinal)
                .ToList(),
        };
    }
}