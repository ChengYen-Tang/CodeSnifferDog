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

    public void ApplySnapshot(AgentStatusSnapshotState snapshot)
    {
        if (snapshot.Snapshot is null)
        {
            SelectedAgentId = null;
            ExpandedToolDetails.Clear();
            return;
        }

        bool selectedAgentStillExists =
            SelectedAgentId is not null &&
            snapshot.ContainsAgent(SelectedAgentId.Value);

        if (selectedAgentStillExists)
            return;

        SelectedAgentId = snapshot.GetFirstAgentId();
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
