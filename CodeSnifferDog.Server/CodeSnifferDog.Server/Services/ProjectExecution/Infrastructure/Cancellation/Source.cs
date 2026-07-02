namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation;

internal enum Source
{
    None = 0,
    UserRequest = 1,
    HostShutdown = 2,
}
