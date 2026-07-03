using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

public sealed class Settings
{
    public const string SectionName = "ProjectExecution";

    public int MaxConcurrentWorkers { get; init; } = 1;

    public int MaxQueuedProjects { get; init; } = 100;

    public ExecutionOptions ExecutionOptions { get; init; } = new();

    public int QueuePollingIntervalSeconds { get; init; } = 2;

    public TimeSpan QueuePollingInterval => TimeSpan.FromSeconds(Math.Max(1, QueuePollingIntervalSeconds));
}
