namespace CodeSnifferDog.Server.Client.Components.AgentStatus.State;

internal sealed class LiveConnectionState(bool isConnected, bool isSubscribed, string statusText)
{
    public bool IsConnected { get; private set; } = isConnected;

    public bool IsSubscribed { get; private set; } = isSubscribed;

    public string StatusText { get; private set; } = statusText;

    public void Update(bool isConnected, bool isSubscribed, string statusText)
    {
        IsConnected = isConnected;
        IsSubscribed = isSubscribed;
        StatusText = statusText;
    }
}

internal sealed class SelectedAgentLiveConnectionState(Guid? agentId, bool isConnected, bool isSubscribed, string statusText)
{
    public Guid? AgentId { get; private set; } = agentId;

    public bool IsConnected { get; private set; } = isConnected;

    public bool IsSubscribed { get; private set; } = isSubscribed;

    public string StatusText { get; private set; } = statusText;

    public void Update(Guid? agentId, bool isConnected, bool isSubscribed, string statusText)
    {
        AgentId = agentId;
        IsConnected = isConnected;
        IsSubscribed = isSubscribed;
        StatusText = statusText;
    }
}
