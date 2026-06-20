namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal interface IAgentStatusRuntimeComponentsFactory
{
    AgentStatusRuntimeComponents Create(Guid projectId);
}
