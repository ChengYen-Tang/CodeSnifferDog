namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

internal sealed record Claim(
    Guid ProjectId,
    string StoredZipRelativePath,
    ProjectExecutionLease ExecutionLease);
