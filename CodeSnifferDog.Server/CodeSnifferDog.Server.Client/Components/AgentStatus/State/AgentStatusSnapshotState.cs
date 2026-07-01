using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed class AgentStatusSnapshotState(ProjectAgentStatusSnapshotDto? snapshot)
{
    private AgentStatusSnapshotLookup _lookup = AgentStatusSnapshotLookup.From(snapshot?.AgentGroups);

    public ProjectAgentStatusSnapshotDto? Snapshot { get; private set; } = snapshot;

    public IReadOnlyList<ProjectAgentGroupSnapshotDto> Groups => Snapshot?.AgentGroups ?? [];

    public void Replace(ProjectAgentStatusSnapshotDto? snapshot)
    {
        Snapshot = snapshot;
        RebuildLookup();
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

    public ProjectAgentSnapshotDto? FindAgent(Guid agentId)
    {
        if (!_lookup.TryGetAgentLocation(agentId, out AgentStatusSnapshotAgentLocation location))
            return null;

        IReadOnlyList<ProjectAgentGroupSnapshotDto> groups = Groups;
        if (location.GroupIndex >= groups.Count)
            return null;

        IReadOnlyList<ProjectAgentSnapshotDto> agents = groups[location.GroupIndex].Agents;
        return location.AgentIndex < agents.Count ? agents[location.AgentIndex] : null;
    }

    public bool ContainsAgent(Guid agentId) => _lookup.ContainsAgent(agentId);

    public Guid? GetFirstAgentId() => _lookup.FirstAgentId;

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
        bool hasExistingGroup = _lookup.TryGetGroupIndex(group.GroupId, out int existingIndex);
        ProjectAgentGroupSnapshotDto nextGroup = hasExistingGroup
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

        if (hasExistingGroup)
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

        if (!_lookup.TryGetGroupIndex(agent.GroupId, out int groupIndex))
            return false;

        List<ProjectAgentGroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        ProjectAgentGroupSnapshotDto group = groups[groupIndex];
        List<ProjectAgentSnapshotDto> agents = group.Agents.ToList();
        bool hasExistingAgent =
            _lookup.TryGetAgentLocation(agent.AgentId, out AgentStatusSnapshotAgentLocation existingLocation) &&
            existingLocation.GroupIndex == groupIndex;
        int existingIndex = hasExistingAgent ? existingLocation.AgentIndex : -1;
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
            hasExistingAgent ? agents[existingIndex].TimelineEntries : [];

        ProjectAgentSnapshotDto nextAgent = new()
        {
            AgentId = agent.AgentId,
            GroupId = agent.GroupId,
            RuntimeKey = agent.RuntimeKey,
            DisplayName = agent.DisplayName,
            Status = agent.Status,
            CreatedAtUtc = agent.CreatedAtUtc,
            SystemPrompt = agent.SystemPrompt,
            HasLoadedHistory = hasExistingAgent && agents[existingIndex].HasLoadedHistory,
            TimelineEntries = timelineEntries,
        };

        if (hasExistingAgent)
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

        if (!_lookup.TryGetAgentLocation(agentStatus.AgentId, out AgentStatusSnapshotAgentLocation location))
            return false;

        List<ProjectAgentGroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        ProjectAgentGroupSnapshotDto group = groups[location.GroupIndex];
        List<ProjectAgentSnapshotDto> agents = group.Agents.ToList();
        ProjectAgentSnapshotDto existingAgent = agents[location.AgentIndex];
        agents[location.AgentIndex] = new ProjectAgentSnapshotDto
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

        groups[location.GroupIndex] = new ProjectAgentGroupSnapshotDto
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

    public bool UpsertTimelineEntry(ProjectAgentTimelineEntryDto? timelineEntry)
    {
        if (timelineEntry is null || Snapshot is null)
            return false;

        if (!_lookup.TryGetAgentLocation(timelineEntry.AgentId, out AgentStatusSnapshotAgentLocation location))
            return false;

        List<ProjectAgentGroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        ProjectAgentGroupSnapshotDto group = groups[location.GroupIndex];
        List<ProjectAgentSnapshotDto> agents = group.Agents.ToList();
        ProjectAgentSnapshotDto agent = agents[location.AgentIndex];
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
            AgentStatusTimelineEntries.Upsert(agent.TimelineEntries, timelineEntry);

        agents[location.AgentIndex] = new ProjectAgentSnapshotDto
        {
            AgentId = agent.AgentId,
            GroupId = agent.GroupId,
            RuntimeKey = agent.RuntimeKey,
            DisplayName = agent.DisplayName,
            SystemPrompt = agent.SystemPrompt,
            Status = agent.Status,
            CreatedAtUtc = agent.CreatedAtUtc,
            HasLoadedHistory = true,
            TimelineEntries = timelineEntries,
        };

        groups[location.GroupIndex] = new ProjectAgentGroupSnapshotDto
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

    public bool RemoveTimelineEntries(Guid agentId, IReadOnlyList<Guid> timelineEntryIds)
    {
        if (Snapshot is null || timelineEntryIds.Count == 0)
            return false;

        if (!_lookup.TryGetAgentLocation(agentId, out AgentStatusSnapshotAgentLocation location))
            return false;

        HashSet<Guid> timelineEntryIdSet = [.. timelineEntryIds];
        List<ProjectAgentGroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        ProjectAgentGroupSnapshotDto group = groups[location.GroupIndex];
        List<ProjectAgentSnapshotDto> agents = group.Agents.ToList();
        ProjectAgentSnapshotDto agent = agents[location.AgentIndex];
        IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries =
            AgentStatusTimelineEntries.Remove(agent.TimelineEntries, timelineEntryIdSet);

        if (timelineEntries.Count == agent.TimelineEntries.Count)
            return false;

        agents[location.AgentIndex] = new ProjectAgentSnapshotDto
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

        groups[location.GroupIndex] = new ProjectAgentGroupSnapshotDto
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

    public void ReplaceAgentHistory(Guid agentId, IReadOnlyList<ProjectAgentTimelineEntryDto> timelineEntries)
    {
        if (Snapshot is null)
            return;

        if (!_lookup.TryGetAgentLocation(agentId, out AgentStatusSnapshotAgentLocation location))
            return;

        List<ProjectAgentGroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        ProjectAgentGroupSnapshotDto group = groups[location.GroupIndex];
        List<ProjectAgentSnapshotDto> agents = group.Agents.ToList();
        ProjectAgentSnapshotDto existingAgent = agents[location.AgentIndex];
        agents[location.AgentIndex] = new ProjectAgentSnapshotDto
        {
            AgentId = existingAgent.AgentId,
            GroupId = existingAgent.GroupId,
            RuntimeKey = existingAgent.RuntimeKey,
            DisplayName = existingAgent.DisplayName,
            SystemPrompt = existingAgent.SystemPrompt,
            Status = existingAgent.Status,
            CreatedAtUtc = existingAgent.CreatedAtUtc,
            HasLoadedHistory = true,
            TimelineEntries = AgentStatusTimelineEntries.Normalize(timelineEntries),
        };

        groups[location.GroupIndex] = new ProjectAgentGroupSnapshotDto
        {
            GroupId = group.GroupId,
            RuntimeKey = group.RuntimeKey,
            DisplayName = group.DisplayName,
            CreatedAtUtc = group.CreatedAtUtc,
            Agents = agents,
        };

        ReplaceSnapshotGroups(groups);
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
        RebuildLookup();
    }

    private void RebuildLookup()
    {
        _lookup = AgentStatusSnapshotLookup.From(Snapshot?.AgentGroups);
    }
}
