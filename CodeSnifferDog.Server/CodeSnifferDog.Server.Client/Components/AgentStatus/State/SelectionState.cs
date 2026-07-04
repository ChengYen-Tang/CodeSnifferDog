namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

/// <summary>
/// Tracks the selected agent and which tool-detail rows are expanded for that agent.
/// </summary>
/// <param name="selectedAgentId">Initially selected agent identifier.</param>
/// <param name="expandedToolDetails">Keys of currently expanded tool-detail rows.</param>
internal sealed class SelectionState(Guid? selectedAgentId, HashSet<string> expandedToolDetails)
{
    /// <summary>
    /// Gets the currently selected agent identifier, if any.
    /// </summary>
    public Guid? SelectedAgentId { get; private set; } = selectedAgentId;

    /// <summary>
    /// Gets the set of expanded tool-detail keys for the selected agent.
    /// </summary>
    public HashSet<string> ExpandedToolDetails { get; } = expandedToolDetails;

    /// <summary>
    /// Selects one agent and clears expanded tool details from the previous selection.
    /// </summary>
    /// <param name="agentId">Agent identifier to select.</param>
    public void SelectAgent(Guid agentId)
    {
        SelectedAgentId = agentId;
        ExpandedToolDetails.Clear();
    }

    /// <summary>
    /// Reconciles the current selection against a replacement snapshot.
    /// </summary>
    /// <param name="snapshot">Snapshot state that defines the currently available agents.</param>
    public void ApplySnapshot(SnapshotState snapshot)
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

    /// <summary>
    /// Toggles the expanded state of one tool-detail key.
    /// </summary>
    /// <param name="key">Tool-detail key to toggle.</param>
    public void ToggleToolDetails(string key)
    {
        if (!ExpandedToolDetails.Add(key))
        {
            ExpandedToolDetails.Remove(key);
        }
    }
}
