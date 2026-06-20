namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

internal interface IAgentStatusRuntimeFactory
{
    AgentStatusRuntime Create(Guid projectId);
}
