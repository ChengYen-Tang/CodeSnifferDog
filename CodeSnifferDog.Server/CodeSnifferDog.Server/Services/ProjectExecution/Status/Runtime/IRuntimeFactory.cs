namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

/// <summary>
/// Creates the runtime state needed to persist project-execution status events for a project.
/// </summary>
internal interface IRuntimeFactory
{
    /// <summary>
    /// Creates the runtime context for the specified project.
    /// </summary>
    /// <param name="projectId">Project identifier whose status events will be persisted.</param>
    /// <returns>The runtime context used by event subscribers.</returns>
    RuntimeContext Create(Guid projectId);
}
