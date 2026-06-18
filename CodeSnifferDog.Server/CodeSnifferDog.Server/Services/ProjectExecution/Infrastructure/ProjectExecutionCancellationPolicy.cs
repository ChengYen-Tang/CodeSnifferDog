namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

internal static class ProjectExecutionCancellationPolicy
{
    public static ProjectExecutionCancellationOutcome Resolve(ProjectExecutionLease lease) =>
        lease.CancellationSource switch
        {
            ProjectExecutionCancellationSource.UserRequest => ProjectExecutionCancellationOutcome.UserCanceled,
            ProjectExecutionCancellationSource.HostShutdown => ProjectExecutionCancellationOutcome.PreserveForRecovery,
            _ => ProjectExecutionCancellationOutcome.UserCanceled,
        };
}
