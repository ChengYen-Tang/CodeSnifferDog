using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

/// <summary>
/// Bundles the concrete services used by the project-execution status runtime.
/// </summary>
/// <param name="EventHandler">Event handler that persists or publishes each status event.</param>
internal sealed record RuntimeComponents(IEventHandler EventHandler);
