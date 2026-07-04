namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

/// <summary>
/// Carries the information required to execute a claimed project.
/// </summary>
/// <param name="ProjectId">Project identifier.</param>
/// <param name="StoredZipRelativePath">Relative path to the uploaded archive.</param>
/// <param name="ExecutionLease">Lease that controls execution lifetime and cancellation.</param>
internal sealed record Claim(
    Guid ProjectId,
    string StoredZipRelativePath,
    Lease ExecutionLease);
