using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

/// <summary>
/// Holds the mutable client-side snapshot tree and applies live updates to it.
/// </summary>
/// <param name="snapshot">Initial snapshot payload.</param>
internal sealed class SnapshotState(StatusSnapshotDto? snapshot)
{
    private SnapshotLookup _lookup = SnapshotLookup.From(snapshot?.AgentGroups);

    /// <summary>
    /// Gets the current snapshot payload, if one has been loaded.
    /// </summary>
    public StatusSnapshotDto? Snapshot { get; private set; } = snapshot;

    /// <summary>
    /// Gets the current snapshot groups, or an empty list when no snapshot exists.
    /// </summary>
    public IReadOnlyList<GroupSnapshotDto> Groups => Snapshot?.AgentGroups ?? [];

    /// <summary>
    /// Replaces the entire snapshot and rebuilds lookup indexes.
    /// </summary>
    /// <param name="snapshot">Replacement snapshot payload.</param>
    public void Replace(StatusSnapshotDto? snapshot)
    {
        Snapshot = snapshot;
        RebuildLookup();
    }

    /// <summary>
    /// Applies one live update that mutates the snapshot tree or project status.
    /// </summary>
    /// <param name="update">Live update to apply.</param>
    /// <returns><see langword="true" /> when the snapshot changed.</returns>
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

    /// <summary>
    /// Finds an agent snapshot by identifier.
    /// </summary>
    /// <param name="agentId">Agent identifier to locate.</param>
    /// <returns>The matching agent snapshot, or <see langword="null" /> when absent.</returns>
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

    /// <summary>
    /// Determines whether the snapshot currently contains the supplied agent.
    /// </summary>
    /// <param name="agentId">Agent identifier to test.</param>
    /// <returns><see langword="true" /> when the agent exists in the snapshot.</returns>
    public bool ContainsAgent(Guid agentId) => _lookup.ContainsAgent(agentId);

    /// <summary>
    /// Gets the first agent identifier present in the snapshot, if any.
    /// </summary>
    /// <returns>The first agent identifier, or <see langword="null" /> when the snapshot is empty.</returns>
    public Guid? GetFirstAgentId() => _lookup.FirstAgentId;

    /// <summary>
    /// Gets cached history entries for the supplied agent.
    /// </summary>
    /// <param name="agentId">Agent identifier whose history should be returned.</param>
    /// <returns>The cached history entries, or an empty list when none exist.</returns>
    public IReadOnlyList<TimelineEntryDto> GetHistory(Guid agentId) =>
        FindAgent(agentId)?.TimelineEntries ?? [];

    /// <summary>
    /// Clears cached history for every agent except the one that should remain loaded.
    /// </summary>
    /// <param name="agentIdToKeep">Agent identifier whose cached history should be preserved.</param>
    public void ReleaseHistoryExcept(Guid? agentIdToKeep)
    {
        if (Snapshot is null)
            return;

        List<GroupSnapshotDto>? groups = null;
        for (int groupIndex = 0; groupIndex < Snapshot.AgentGroups.Count; groupIndex++)
        {
            GroupSnapshotDto group = Snapshot.AgentGroups[groupIndex];
            List<SnapshotDto>? agents = null;
            for (int agentIndex = 0; agentIndex < group.Agents.Count; agentIndex++)
            {
                SnapshotDto agent = group.Agents[agentIndex];
                if (agent.AgentId == agentIdToKeep ||
                    (!agent.HasLoadedHistory && agent.TimelineEntries.Count == 0 && agent.SystemPrompt is null))
                {
                    continue;
                }

                agents ??= group.Agents.ToList();
                agents[agentIndex] = new SnapshotDto
                {
                    AgentId = agent.AgentId,
                    GroupId = agent.GroupId,
                    RuntimeKey = agent.RuntimeKey,
                    DisplayName = agent.DisplayName,
                    SystemPrompt = null,
                    Status = agent.Status,
                    CreatedAtUtc = agent.CreatedAtUtc,
                    HasLoadedHistory = false,
                    TimelineEntries = [],
                };
            }

            if (agents is null)
                continue;

            groups ??= Snapshot.AgentGroups.ToList();
            groups[groupIndex] = new GroupSnapshotDto
            {
                GroupId = group.GroupId,
                RuntimeKey = group.RuntimeKey,
                DisplayName = group.DisplayName,
                CreatedAtUtc = group.CreatedAtUtc,
                Agents = agents,
            };
        }

        if (groups is not null)
            ReplaceSnapshotGroupsPreservingLookup(groups);
    }

    /// <summary>
    /// Inserts or replaces one group node in the snapshot tree.
    /// </summary>
    /// <param name="group">Live group payload to upsert.</param>
    /// <returns><see langword="true" /> when the snapshot changed.</returns>
    private bool UpsertGroup(GroupLiveDto? group)
    {
        if (group is null || Snapshot is null)
            return false;

        bool hasExistingGroup = _lookup.TryGetGroupIndex(group.GroupId, out int existingIndex);
        if (hasExistingGroup)
        {
            GroupSnapshotDto existingGroup = Snapshot.AgentGroups[existingIndex];
            if (string.Equals(existingGroup.RuntimeKey, group.RuntimeKey, StringComparison.Ordinal) &&
                string.Equals(existingGroup.DisplayName, group.DisplayName, StringComparison.Ordinal) &&
                existingGroup.CreatedAtUtc == group.CreatedAtUtc)
            {
                return false;
            }
        }

        List<GroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
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

    /// <summary>
    /// Inserts or replaces one agent node in the snapshot tree while preserving loaded history when possible.
    /// </summary>
    /// <param name="agent">Live agent payload to upsert.</param>
    /// <returns><see langword="true" /> when the snapshot changed.</returns>
    private bool UpsertAgent(LiveDto? agent)
    {
        if (agent is null || Snapshot is null)
            return false;

        if (!_lookup.TryGetGroupIndex(agent.GroupId, out int groupIndex))
            return false;

        GroupSnapshotDto group = Snapshot.AgentGroups[groupIndex];
        bool hasExistingAgent =
            _lookup.TryGetAgentLocation(agent.AgentId, out AgentStatusSnapshotAgentLocation existingLocation) &&
            existingLocation.GroupIndex == groupIndex;
        int existingIndex = hasExistingAgent ? existingLocation.AgentIndex : -1;
        SnapshotDto? existingAgent = hasExistingAgent ? group.Agents[existingIndex] : null;
        IReadOnlyList<TimelineEntryDto> timelineEntries = existingAgent?.TimelineEntries ?? [];

        if (existingAgent is not null && AgentMatches(existingAgent, agent))
            return false;

        List<GroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        List<SnapshotDto> agents = group.Agents.ToList();
        SnapshotDto nextAgent = new()
        {
            AgentId = agent.AgentId,
            GroupId = agent.GroupId,
            RuntimeKey = agent.RuntimeKey,
            DisplayName = agent.DisplayName,
            Status = agent.Status,
            CreatedAtUtc = agent.CreatedAtUtc,
            SystemPrompt = agent.SystemPrompt ?? existingAgent?.SystemPrompt,
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

    /// <summary>
    /// Updates one agent's status field inside the snapshot tree.
    /// </summary>
    /// <param name="agentStatus">Live agent-status payload to apply.</param>
    /// <returns><see langword="true" /> when the snapshot changed.</returns>
    private bool UpdateAgentStatus(StatusChangedDto? agentStatus)
    {
        if (agentStatus is null || Snapshot is null)
            return false;

        if (!_lookup.TryGetAgentLocation(agentStatus.AgentId, out AgentStatusSnapshotAgentLocation location))
            return false;

        GroupSnapshotDto group = Snapshot.AgentGroups[location.GroupIndex];
        SnapshotDto existingAgent = group.Agents[location.AgentIndex];
        if (existingAgent.Status == agentStatus.Status)
            return false;

        List<GroupSnapshotDto> groups = Snapshot.AgentGroups.ToList();
        List<SnapshotDto> agents = group.Agents.ToList();
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

        ReplaceSnapshotGroupsPreservingLookup(groups);
        return true;
    }

    /// <summary>
    /// Upserts one timeline entry into the cached history of its agent.
    /// </summary>
    /// <param name="timelineEntry">Timeline entry to insert or replace.</param>
    /// <param name="latestSequence">Latest sequence already known for the selected history view.</param>
    /// <returns>The rewritten history and latest sequence, or <see langword="null" /> when the agent is absent.</returns>
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

        ReplaceSnapshotGroupsPreservingLookup(groups);
        return result;
    }

    /// <summary>
    /// Removes cached timeline entries by identifier for one agent.
    /// </summary>
    /// <param name="agentId">Agent identifier whose cached history should be updated.</param>
    /// <param name="timelineEntryIds">Identifiers of timeline entries to remove.</param>
    /// <returns>The rewritten history and latest sequence, or <see langword="null" /> when nothing changed.</returns>
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

        ReplaceSnapshotGroupsPreservingLookup(groups);
        return result;
    }

    /// <summary>
    /// Replaces cached history for one agent with normalized timeline entries.
    /// </summary>
    /// <param name="agentId">Agent identifier whose cached history should be replaced.</param>
    /// <param name="timelineEntries">Replacement timeline entries.</param>
    public void ReplaceAgentHistory(
        Guid agentId,
        IReadOnlyList<TimelineEntryDto> timelineEntries,
        string? systemPrompt = null)
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
            SystemPrompt = systemPrompt ?? existingAgent.SystemPrompt,
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

        ReplaceSnapshotGroupsPreservingLookup(groups);
    }

    /// <summary>
    /// Updates the project execution status without rebuilding group data.
    /// </summary>
    /// <param name="projectStatus">Live project-status payload to apply.</param>
    /// <returns><see langword="true" /> when the snapshot changed.</returns>
    private bool UpdateProjectStatus(ExecutionStatusChangedDto? projectStatus)
    {
        if (projectStatus is null || Snapshot is null)
            return false;

        if (Snapshot.ProjectStatus == projectStatus.Status)
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

    /// <summary>
    /// Replaces the snapshot group tree, normalizes group ordering, and rebuilds lookup indexes.
    /// </summary>
    /// <param name="groups">Replacement snapshot groups.</param>
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

    /// <summary>
    /// Replaces group nodes after a value-only mutation that preserves every group and agent index.
    /// </summary>
    private void ReplaceSnapshotGroupsPreservingLookup(IReadOnlyList<GroupSnapshotDto> groups)
    {
        if (Snapshot is null)
            return;

        Snapshot = new StatusSnapshotDto
        {
            ProjectId = Snapshot.ProjectId,
            ProjectStatus = Snapshot.ProjectStatus,
            SnapshotGeneratedAtUtc = Snapshot.SnapshotGeneratedAtUtc,
            AgentGroups = groups,
        };
    }

    /// <summary>
    /// Rebuilds fast lookup indexes from the current snapshot tree.
    /// </summary>
    private void RebuildLookup()
    {
        _lookup = SnapshotLookup.From(Snapshot?.AgentGroups);
    }

    /// <summary>
    /// Determines whether a live agent summary is identical to the current snapshot node.
    /// </summary>
    private static bool AgentMatches(SnapshotDto snapshot, LiveDto update) =>
        snapshot.AgentId == update.AgentId &&
        snapshot.GroupId == update.GroupId &&
        string.Equals(snapshot.RuntimeKey, update.RuntimeKey, StringComparison.Ordinal) &&
        string.Equals(snapshot.DisplayName, update.DisplayName, StringComparison.Ordinal) &&
        (update.SystemPrompt is null ||
            string.Equals(snapshot.SystemPrompt, update.SystemPrompt, StringComparison.Ordinal)) &&
        snapshot.Status == update.Status &&
        snapshot.CreatedAtUtc == update.CreatedAtUtc;
}
