using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

internal sealed record RuntimeComponents(IEventHandler EventHandler);
