using CodeSnifferDog.Server.Shared.AgentStatus;

namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed class AgentStatusSelectionState(Guid? selectedAgentId, HashSet<string> expandedToolDetails)
{
    public Guid? SelectedAgentId { get; private set; } = selectedAgentId;

    public HashSet<string> ExpandedToolDetails { get; } = expandedToolDetails;

    public void SelectAgent(Guid agentId)
    {
        SelectedAgentId = agentId;
        ExpandedToolDetails.Clear();
    }

    public void ApplySnapshot(ProjectAgentStatusSnapshotDto? snapshot)
    {
        if (snapshot is null)
        {
            SelectedAgentId = null;
            ExpandedToolDetails.Clear();
            return;
        }

        bool selectedAgentStillExists =
            SelectedAgentId is not null &&
            snapshot.AgentGroups.SelectMany(group => group.Agents).Any(agent => agent.AgentId == SelectedAgentId.Value);

        if (selectedAgentStillExists)
            return;

        SelectedAgentId = snapshot.AgentGroups
            .SelectMany(group => group.Agents)
            .Select(agent => (Guid?)agent.AgentId)
            .FirstOrDefault();
        ExpandedToolDetails.Clear();
    }

    public void ToggleToolDetails(string key)
    {
        if (!ExpandedToolDetails.Add(key))
        {
            ExpandedToolDetails.Remove(key);
        }
    }
}