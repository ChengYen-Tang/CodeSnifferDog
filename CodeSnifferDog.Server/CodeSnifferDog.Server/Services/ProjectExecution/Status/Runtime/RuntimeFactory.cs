namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

internal sealed class RuntimeFactory(IRuntimeComponentsFactory componentsFactory) : IRuntimeFactory
{
    private readonly IRuntimeComponentsFactory _componentsFactory = componentsFactory;

    public RuntimeContext Create(Guid projectId)
    {
        RuntimeComponents components = _componentsFactory.Create(projectId);
        return new RuntimeContext(components.EventHandler);
    }
}
