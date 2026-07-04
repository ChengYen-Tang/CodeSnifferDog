namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

/// <summary>
/// Creates runtime contexts that expose status persistence services for a project.
/// </summary>
internal sealed class RuntimeFactory(IRuntimeComponentsFactory componentsFactory) : IRuntimeFactory
{
    private readonly IRuntimeComponentsFactory _componentsFactory = componentsFactory;

    /// <inheritdoc />
    public RuntimeContext Create(Guid projectId)
    {
        RuntimeComponents components = _componentsFactory.Create(projectId);
        return new RuntimeContext(components.EventHandler);
    }
}
