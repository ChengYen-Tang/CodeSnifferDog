namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

internal interface IAgentStatusRuntimeComponentsFactory
{
    AgentStatusRuntimeComponents Create(Guid projectId);
}
