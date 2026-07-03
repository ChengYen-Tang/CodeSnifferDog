namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation;

internal static class Policy
{
    public static Outcome Resolve(Lease lease) =>
        lease.CancellationSource switch
        {
            Source.UserRequest => Outcome.UserCanceled,
            Source.HostShutdown => Outcome.PreserveForRecovery,
            _ => Outcome.UserCanceled,
        };
}
