using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

/// <summary>
/// Carries the runtime services needed while subscribing to project status events.
/// </summary>
/// <param name="EventHandler">Event handler that persists or publishes each status event.</param>
internal sealed record RuntimeContext(IEventHandler EventHandler);
