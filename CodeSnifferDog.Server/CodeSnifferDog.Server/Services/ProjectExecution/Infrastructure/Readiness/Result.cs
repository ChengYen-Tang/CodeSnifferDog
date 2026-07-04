namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Readiness;

/// <summary>
/// Reports whether project execution is ready to start.
/// </summary>
/// <param name="IsReady">Whether execution can start.</param>
/// <param name="Reason">Optional explanation when execution cannot start.</param>
internal sealed record Result(bool IsReady, string? Reason);
