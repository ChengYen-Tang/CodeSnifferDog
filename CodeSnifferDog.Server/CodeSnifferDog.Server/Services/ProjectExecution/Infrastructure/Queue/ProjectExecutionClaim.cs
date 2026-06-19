namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

internal sealed record ProjectExecutionClaim(
    Guid ProjectId,
    string StoredZipRelativePath,
    ProjectExecutionLease ExecutionLease);
