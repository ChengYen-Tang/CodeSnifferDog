namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

/// <summary>
/// Tracks the overall live connection state shown by the page.
/// </summary>
/// <param name="isConnected">Initial transport connection state.</param>
/// <param name="isSubscribed">Initial subscription state.</param>
/// <param name="statusText">Initial UI status text.</param>
internal sealed class LiveConnectionState(bool isConnected, bool isSubscribed, string statusText)
{
    /// <summary>
    /// Gets whether the live-update transport is connected.
    /// </summary>
    public bool IsConnected { get; private set; } = isConnected;

    /// <summary>
    /// Gets whether the page is currently subscribed to live updates.
    /// </summary>
    public bool IsSubscribed { get; private set; } = isSubscribed;

    /// <summary>
    /// Gets the UI status text for the current connection state.
    /// </summary>
    public string StatusText { get; private set; } = statusText;

    /// <summary>
    /// Replaces the connection state values.
    /// </summary>
    /// <param name="isConnected">Replacement transport connection state.</param>
    /// <param name="isSubscribed">Replacement subscription state.</param>
    /// <param name="statusText">Replacement UI status text.</param>
    public void Update(bool isConnected, bool isSubscribed, string statusText)
    {
        IsConnected = isConnected;
        IsSubscribed = isSubscribed;
        StatusText = statusText;
    }
}

/// <summary>
/// Tracks live connection state scoped to the currently selected agent.
/// </summary>
/// <param name="agentId">Initially associated selected agent identifier.</param>
/// <param name="isConnected">Initial transport connection state.</param>
/// <param name="isSubscribed">Initial subscription state.</param>
/// <param name="statusText">Initial UI status text.</param>
internal sealed class SelectedAgentLiveConnectionState(Guid? agentId, bool isConnected, bool isSubscribed, string statusText)
{
    /// <summary>
    /// Gets the selected agent identifier associated with this connection state.
    /// </summary>
    public Guid? AgentId { get; private set; } = agentId;

    /// <summary>
    /// Gets whether the selected-agent live connection is connected.
    /// </summary>
    public bool IsConnected { get; private set; } = isConnected;

    /// <summary>
    /// Gets whether the selected-agent live connection is subscribed.
    /// </summary>
    public bool IsSubscribed { get; private set; } = isSubscribed;

    /// <summary>
    /// Gets the UI status text for the selected-agent connection state.
    /// </summary>
    public string StatusText { get; private set; } = statusText;

    /// <summary>
    /// Replaces the selected-agent connection state values.
    /// </summary>
    /// <param name="agentId">Selected agent identifier associated with the replacement state.</param>
    /// <param name="isConnected">Replacement transport connection state.</param>
    /// <param name="isSubscribed">Replacement subscription state.</param>
    /// <param name="statusText">Replacement UI status text.</param>
    public void Update(Guid? agentId, bool isConnected, bool isSubscribed, string statusText)
    {
        AgentId = agentId;
        IsConnected = isConnected;
        IsSubscribed = isSubscribed;
        StatusText = statusText;
    }
}
