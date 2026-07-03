namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Readiness;

internal sealed record Result(bool IsReady, string? Reason);
