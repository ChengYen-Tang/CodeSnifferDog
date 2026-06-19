namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Readiness;

internal sealed record ExecutionReadinessResult(bool IsReady, string? Reason);
