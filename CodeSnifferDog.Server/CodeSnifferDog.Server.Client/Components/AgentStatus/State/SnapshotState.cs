using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed class SnapshotState(StatusSnapshotDto? snapshot)
{
    private SnapshotLookup _lookup = SnapshotLookup.From(snapshot?.AgentGroups);

    public StatusSnapshotDto? Snapshot { get; private set; } = snapshot;

    public IReadOnlyList<GroupSnapshotDto> Groups => Snapshot?.AgentGroups ?? [];

    public void Replace(StatusSnapshotDto? snapshot)
    {
        Snapshot = snapshot;
        RebuildLookup();
    }

    public bool ApplyLiveUpdate(LiveUpdateDto update)
    {
        if (Snapshot is null)
            return false;

        return update.Kind switch
        {
            LiveUpdateKind.ProjectStatusChanged => UpdateProjectStatus(update.ProjectStatus),
            LiveUpdateKind.AgentGroupUpserted => UpsertGroup(update.Group),
            LiveUpdateKind.AgentUpserted => UpsertAgent(update.Agent),
            LiveUpdateKind.AgentStatusChanged => UpdateAgentStatus(update.AgentStatus),
            _ => false,
        };
    }

    public SnapshotDto? FindAgent(Guid agentId)
    {
        if (!_lookup.TryGetAgentLocation(agentId, out AgentStatusSnapshotAgentLocation location))
            return null;

        IReadOnlyList<GroupSnapshotDto> groups = Groups;
        if (location.GroupIndex >= groups.Count)
            return null;

        IReadOnlyList<SnapshotDto> agents = groups[location.GroupIndex].Agents;
        return location.AgentIndex < agents.Count ? agents[location.AgentIndex] : null;
    }

    public bool ContainsAgent(Guid agentId) => _lookup.ContainsAgent(agentId);

    public Guid? GetFirstAgentId() => _lookup.FirstAgentId;

    public IReadOnlyList<TimelineEntryDto> GetHistory(Guid agentId) =>
        FindAgent(agentId)?.TimelineEntries ?? [];

    public void ReleaseHistoryExcept(Guid? agentIdToKeep)
    {
        if (Snapshot is null)
            return;

        List<GroupSnapshotDto> groups = Snapshot.AgentGroups
            .Select(group => new GroupSnapshotDto
            {
                GroupId = group.GroupId,
                RuntimeKey = group.RuntimeKey,
                DisplayName = group.DisplayName,
                CreatedAtUtc = group.CreatedAtUtc,
                Agents = group.Agents
                    .Select(agent => new SnapshotDto
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

    private bool UpsertGroup(GroupLiveDto? group)
    {
        if (group is null || Snapshot is null)
            return false;

        List<GroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        bool hasExistingGroup = _lookup.TryGetGroupIndex(group.GroupId, out int existingIndex);
        GroupSnapshotDto nextGroup = hasExistingGroup
            ? new GroupSnapshotDto
            {
                GroupId = group.GroupId,
                RuntimeKey = group.RuntimeKey,
                DisplayName = group.DisplayName,
                CreatedAtUtc = group.CreatedAtUtc,
                Agents = groups[existingIndex].Agents,
            }
            : new GroupSnapshotDto
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

    private bool UpsertAgent(LiveDto? agent)
    {
        if (agent is null || Snapshot is null)
            return false;

        if (!_lookup.TryGetGroupIndex(agent.GroupId, out int groupIndex))
            return false;

        List<GroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        GroupSnapshotDto group = groups[groupIndex];
        List<SnapshotDto> agents = group.Agents.ToList();
        bool hasExistingAgent =
            _lookup.TryGetAgentLocation(agent.AgentId, out AgentStatusSnapshotAgentLocation existingLocation) &&
            existingLocation.GroupIndex == groupIndex;
        int existingIndex = hasExistingAgent ? existingLocation.AgentIndex : -1;
        IReadOnlyList<TimelineEntryDto> timelineEntries =
            hasExistingAgent ? agents[existingIndex].TimelineEntries : [];

        SnapshotDto nextAgent = new()
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

        groups[groupIndex] = new GroupSnapshotDto
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

    private bool UpdateAgentStatus(StatusChangedDto? agentStatus)
    {
        if (agentStatus is null || Snapshot is null)
            return false;

        if (!_lookup.TryGetAgentLocation(agentStatus.AgentId, out AgentStatusSnapshotAgentLocation location))
            return false;

        List<GroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        GroupSnapshotDto group = groups[location.GroupIndex];
        List<SnapshotDto> agents = group.Agents.ToList();
        SnapshotDto existingAgent = agents[location.AgentIndex];
        agents[location.AgentIndex] = new SnapshotDto
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

        groups[location.GroupIndex] = new GroupSnapshotDto
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

    public TimelineMutationResult? UpsertTimelineEntry(
        TimelineEntryDto? timelineEntry,
        long latestSequence)
    {
        if (timelineEntry is null || Snapshot is null)
            return null;

        if (!_lookup.TryGetAgentLocation(timelineEntry.AgentId, out AgentStatusSnapshotAgentLocation location))
            return null;

        List<GroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        GroupSnapshotDto group = groups[location.GroupIndex];
        List<SnapshotDto> agents = group.Agents.ToList();
        SnapshotDto agent = agents[location.AgentIndex];
        TimelineMutationResult result =
            TimelineEntryList.UpsertWithLatestSequence(agent.TimelineEntries, timelineEntry, latestSequence);

        agents[location.AgentIndex] = new SnapshotDto
        {
            AgentId = agent.AgentId,
            GroupId = agent.GroupId,
            RuntimeKey = agent.RuntimeKey,
            DisplayName = agent.DisplayName,
            SystemPrompt = agent.SystemPrompt,
            Status = agent.Status,
            CreatedAtUtc = agent.CreatedAtUtc,
            HasLoadedHistory = true,
            TimelineEntries = result.TimelineEntries,
        };

        groups[location.GroupIndex] = new GroupSnapshotDto
        {
            GroupId = group.GroupId,
            RuntimeKey = group.RuntimeKey,
            DisplayName = group.DisplayName,
            CreatedAtUtc = group.CreatedAtUtc,
            Agents = agents,
        };

        ReplaceSnapshotGroups(groups);
        return result;
    }

    public TimelineMutationResult? RemoveTimelineEntries(Guid agentId, IReadOnlyList<Guid> timelineEntryIds)
    {
        if (Snapshot is null || timelineEntryIds.Count == 0)
            return null;

        if (!_lookup.TryGetAgentLocation(agentId, out AgentStatusSnapshotAgentLocation location))
            return null;

        HashSet<Guid> timelineEntryIdSet = [.. timelineEntryIds];
        List<GroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        GroupSnapshotDto group = groups[location.GroupIndex];
        List<SnapshotDto> agents = group.Agents.ToList();
        SnapshotDto agent = agents[location.AgentIndex];
        TimelineMutationResult? result =
            TimelineEntryList.RemoveWithLatestSequence(agent.TimelineEntries, timelineEntryIdSet);
        if (result is null)
            return null;

        agents[location.AgentIndex] = new SnapshotDto
        {
            AgentId = agent.AgentId,
            GroupId = agent.GroupId,
            RuntimeKey = agent.RuntimeKey,
            DisplayName = agent.DisplayName,
            SystemPrompt = agent.SystemPrompt,
            Status = agent.Status,
            CreatedAtUtc = agent.CreatedAtUtc,
            HasLoadedHistory = agent.HasLoadedHistory,
            TimelineEntries = result.TimelineEntries,
        };

        groups[location.GroupIndex] = new GroupSnapshotDto
        {
            GroupId = group.GroupId,
            RuntimeKey = group.RuntimeKey,
            DisplayName = group.DisplayName,
            CreatedAtUtc = group.CreatedAtUtc,
            Agents = agents,
        };

        ReplaceSnapshotGroups(groups);
        return result;
    }

    public void ReplaceAgentHistory(Guid agentId, IReadOnlyList<TimelineEntryDto> timelineEntries)
    {
        if (Snapshot is null)
            return;

        if (!_lookup.TryGetAgentLocation(agentId, out AgentStatusSnapshotAgentLocation location))
            return;

        List<GroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        GroupSnapshotDto group = groups[location.GroupIndex];
        List<SnapshotDto> agents = group.Agents.ToList();
        SnapshotDto existingAgent = agents[location.AgentIndex];
        agents[location.AgentIndex] = new SnapshotDto
        {
            AgentId = existingAgent.AgentId,
            GroupId = existingAgent.GroupId,
            RuntimeKey = existingAgent.RuntimeKey,
            DisplayName = existingAgent.DisplayName,
            SystemPrompt = existingAgent.SystemPrompt,
            Status = existingAgent.Status,
            CreatedAtUtc = existingAgent.CreatedAtUtc,
            HasLoadedHistory = true,
            TimelineEntries = TimelineEntryList.Normalize(timelineEntries),
        };

        groups[location.GroupIndex] = new GroupSnapshotDto
        {
            GroupId = group.GroupId,
            RuntimeKey = group.RuntimeKey,
            DisplayName = group.DisplayName,
            CreatedAtUtc = group.CreatedAtUtc,
            Agents = agents,
        };

        ReplaceSnapshotGroups(groups);
    }

    private bool UpdateProjectStatus(ExecutionStatusChangedDto? projectStatus)
    {
        if (projectStatus is null || Snapshot is null)
            return false;

        Snapshot = new StatusSnapshotDto
        {
            ProjectId = Snapshot.ProjectId,
            ProjectStatus = projectStatus.Status,
            SnapshotGeneratedAtUtc = Snapshot.SnapshotGeneratedAtUtc,
            AgentGroups = Snapshot.AgentGroups,
        };

        return true;
    }

    private void ReplaceSnapshotGroups(IReadOnlyList<GroupSnapshotDto> groups)
    {
        if (Snapshot is null)
            return;

        Snapshot = new StatusSnapshotDto
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
        _lookup = SnapshotLookup.From(Snapshot?.AgentGroups);
    }
}
