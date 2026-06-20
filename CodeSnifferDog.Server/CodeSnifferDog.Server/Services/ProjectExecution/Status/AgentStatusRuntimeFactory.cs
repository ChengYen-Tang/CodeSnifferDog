namespace CodeSnifferDog.Server.Services.ProjectExecution.Status;

internal sealed class AgentStatusRuntimeFactory(IAgentStatusRuntimeComponentsFactory componentsFactory) : IAgentStatusRuntimeFactory
{
    private readonly IAgentStatusRuntimeComponentsFactory _componentsFactory = componentsFactory;

    public AgentStatusRuntime Create(Guid projectId)
    {
        AgentStatusRuntimeComponents components = _componentsFactory.Create(projectId);
        return new AgentStatusRuntime(components.EventHandler);
    }
}
