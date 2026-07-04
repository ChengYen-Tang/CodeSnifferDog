using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

/// <summary>
/// Provides fast lookup indexes from snapshot group and agent identifiers back to their positions in the snapshot tree.
/// </summary>
internal sealed class SnapshotLookup
{
    private static readonly SnapshotLookup Empty = new(
        new Dictionary<Guid, int>(),
        new Dictionary<Guid, AgentStatusSnapshotAgentLocation>(),
        null);

    private readonly IReadOnlyDictionary<Guid, int> _groupIndexesById;
    private readonly IReadOnlyDictionary<Guid, AgentStatusSnapshotAgentLocation> _agentLocationsById;

    private SnapshotLookup(
        IReadOnlyDictionary<Guid, int> groupIndexesById,
        IReadOnlyDictionary<Guid, AgentStatusSnapshotAgentLocation> agentLocationsById,
        Guid? firstAgentId)
    {
        _groupIndexesById = groupIndexesById;
        _agentLocationsById = agentLocationsById;
        FirstAgentId = firstAgentId;
    }

    /// <summary>
    /// Gets the first agent identifier found while building the lookup, or <see langword="null" /> when the snapshot is empty.
    /// </summary>
    public Guid? FirstAgentId { get; }

    /// <summary>
    /// Builds a lookup from the supplied snapshot groups.
    /// </summary>
    /// <param name="groups">Snapshot groups to index.</param>
    /// <returns>A lookup capable of resolving group and agent positions.</returns>
    public static SnapshotLookup From(IReadOnlyList<GroupSnapshotDto>? groups)
    {
        if (groups is null || groups.Count == 0)
            return Empty;

        Dictionary<Guid, int> groupIndexesById = new(groups.Count);
        Dictionary<Guid, AgentStatusSnapshotAgentLocation> agentLocationsById = [];
        Guid? firstAgentId = null;

        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            GroupSnapshotDto group = groups[groupIndex];
            groupIndexesById[group.GroupId] = groupIndex;

            for (int agentIndex = 0; agentIndex < group.Agents.Count; agentIndex++)
            {
                SnapshotDto agent = group.Agents[agentIndex];
                firstAgentId ??= agent.AgentId;
                agentLocationsById[agent.AgentId] = new AgentStatusSnapshotAgentLocation(groupIndex, agentIndex);
            }
        }

        return new SnapshotLookup(groupIndexesById, agentLocationsById, firstAgentId);
    }

    /// <summary>
    /// Attempts to resolve a group identifier to its snapshot list index.
    /// </summary>
    /// <param name="groupId">Group identifier to resolve.</param>
    /// <param name="groupIndex">Resolved group index when the group exists.</param>
    /// <returns><see langword="true" /> when the group exists in the snapshot.</returns>
    public bool TryGetGroupIndex(Guid groupId, out int groupIndex) =>
        _groupIndexesById.TryGetValue(groupId, out groupIndex);

    /// <summary>
    /// Attempts to resolve an agent identifier to its group and agent indexes inside the snapshot tree.
    /// </summary>
    /// <param name="agentId">Agent identifier to resolve.</param>
    /// <param name="location">Resolved group/agent location when the agent exists.</param>
    /// <returns><see langword="true" /> when the agent exists in the snapshot.</returns>
    public bool TryGetAgentLocation(Guid agentId, out AgentStatusSnapshotAgentLocation location) =>
        _agentLocationsById.TryGetValue(agentId, out location);

    /// <summary>
    /// Determines whether the snapshot currently contains the supplied agent.
    /// </summary>
    /// <param name="agentId">Agent identifier to test.</param>
    /// <returns><see langword="true" /> when the agent exists in the snapshot.</returns>
    public bool ContainsAgent(Guid agentId) => _agentLocationsById.ContainsKey(agentId);
}

/// <summary>
/// Identifies an agent's group index and agent index inside the snapshot tree.
/// </summary>
/// <param name="GroupIndex">Zero-based group index.</param>
/// <param name="AgentIndex">Zero-based agent index inside the owning group.</param>
internal readonly record struct AgentStatusSnapshotAgentLocation(int GroupIndex, int AgentIndex);
