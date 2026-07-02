using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

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

    public Guid? FirstAgentId { get; }

    public static SnapshotLookup From(IReadOnlyList<ProjectAgentGroupSnapshotDto>? groups)
    {
        if (groups is null || groups.Count == 0)
            return Empty;

        Dictionary<Guid, int> groupIndexesById = new(groups.Count);
        Dictionary<Guid, AgentStatusSnapshotAgentLocation> agentLocationsById = [];
        Guid? firstAgentId = null;

        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            ProjectAgentGroupSnapshotDto group = groups[groupIndex];
            groupIndexesById[group.GroupId] = groupIndex;

            for (int agentIndex = 0; agentIndex < group.Agents.Count; agentIndex++)
            {
                ProjectAgentSnapshotDto agent = group.Agents[agentIndex];
                firstAgentId ??= agent.AgentId;
                agentLocationsById[agent.AgentId] = new AgentStatusSnapshotAgentLocation(groupIndex, agentIndex);
            }
        }

        return new SnapshotLookup(groupIndexesById, agentLocationsById, firstAgentId);
    }

    public bool TryGetGroupIndex(Guid groupId, out int groupIndex) =>
        _groupIndexesById.TryGetValue(groupId, out groupIndex);

    public bool TryGetAgentLocation(Guid agentId, out AgentStatusSnapshotAgentLocation location) =>
        _agentLocationsById.TryGetValue(agentId, out location);

    public bool ContainsAgent(Guid agentId) => _agentLocationsById.ContainsKey(agentId);
}

internal readonly record struct AgentStatusSnapshotAgentLocation(int GroupIndex, int AgentIndex);
