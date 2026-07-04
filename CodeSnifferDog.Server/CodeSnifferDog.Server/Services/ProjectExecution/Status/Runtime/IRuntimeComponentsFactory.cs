namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

/// <summary>
/// Creates the concrete services used by the project-execution status runtime.
/// </summary>
internal interface IRuntimeComponentsFactory
{
    /// <summary>
    /// Creates the runtime components for the specified project.
    /// </summary>
    /// <param name="projectId">Project identifier whose status events will be persisted.</param>
    /// <returns>The runtime components bound to the project.</returns>
    RuntimeComponents Create(Guid projectId);
}
