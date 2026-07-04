namespace CodeSnifferDog.Server.Shared.AgentStatus;

/// <summary>
/// Represents the client connection state for the agent-status live stream.
/// </summary>
public enum AgentStatusConnectionState
{
    /// <summary>
    /// The client is disconnected.
    /// </summary>
    Disconnected = 0,

    /// <summary>
    /// The client is establishing a connection.
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// The client is connected.
    /// </summary>
    Connected = 2,
}
