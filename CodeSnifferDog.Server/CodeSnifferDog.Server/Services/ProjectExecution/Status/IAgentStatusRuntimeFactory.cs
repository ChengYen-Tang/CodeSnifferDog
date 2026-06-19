namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal interface IAgentStatusRuntimeFactory
{
    AgentStatusRuntime Create(Guid projectId);
}
