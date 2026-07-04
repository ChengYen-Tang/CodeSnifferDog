namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation;

/// <summary>
/// Maps lease cancellation sources to the cleanup policy that should run afterward.
/// </summary>
internal static class Policy
{
    /// <summary>
    /// Resolves the cancellation outcome for a running execution lease.
    /// </summary>
    /// <param name="lease">Lease whose cancellation source should be evaluated.</param>
    /// <returns>The cleanup and persistence outcome to apply.</returns>
    public static Outcome Resolve(Lease lease) =>
        lease.CancellationSource switch
        {
            Source.UserRequest => Outcome.UserCanceled,
            Source.HostShutdown => Outcome.PreserveForRecovery,
            _ => Outcome.UserCanceled,
        };
}
