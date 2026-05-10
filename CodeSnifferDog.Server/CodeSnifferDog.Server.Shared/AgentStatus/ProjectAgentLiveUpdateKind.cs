namespace CodeSnifferDog.Server.Shared.AgentStatus;

public enum ProjectAgentLiveUpdateKind
{
    AgentGroupUpserted = 1,
    AgentUpserted = 2,
    AgentStatusChanged = 3,
    TimelineEntryUpserted = 4,
}
