namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

internal enum ProjectExecutionCancellationSource
{
    None = 0,
    UserRequest = 1,
    HostShutdown = 2,
}
