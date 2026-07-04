namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation;

/// <summary>
/// Identifies the source that canceled a running project execution.
/// </summary>
internal enum Source
{
    /// <summary>
    /// No cancellation source has been recorded.
    /// </summary>
    None = 0,

    /// <summary>
    /// Execution was canceled by an explicit user request.
    /// </summary>
    UserRequest = 1,

    /// <summary>
    /// Execution was canceled because the host is shutting down.
    /// </summary>
    HostShutdown = 2,
}
