using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

/// <summary>
/// Stores configuration for the background project execution service.
/// </summary>
public sealed class Settings
{
    /// <summary>
    /// Gets the configuration section name for project execution settings.
    /// </summary>
    public const string SectionName = "ProjectExecution";

    /// <summary>
    /// Gets the maximum number of background workers that can execute projects concurrently.
    /// </summary>
    public int MaxConcurrentWorkers { get; init; } = 1;

    /// <summary>
    /// Gets the maximum number of projects that may remain queued.
    /// </summary>
    public int MaxQueuedProjects { get; init; } = 100;

    /// <summary>
    /// Gets the workflow execution limits applied to each project.
    /// </summary>
    public ExecutionOptions ExecutionOptions { get; init; } = new();

    /// <summary>
    /// Gets the queue polling interval, in seconds.
    /// </summary>
    public int QueuePollingIntervalSeconds { get; init; } = 2;

    /// <summary>
    /// Gets the normalized queue polling interval.
    /// </summary>
    public TimeSpan QueuePollingInterval => TimeSpan.FromSeconds(Math.Max(1, QueuePollingIntervalSeconds));
}
