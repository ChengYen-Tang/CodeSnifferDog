namespace CodeSnifferDog.Server.Shared.Projects;

/// <summary>
/// Represents the user-facing processing state of a project.
/// </summary>
public enum ProjectStatus
{
    /// <summary>
    /// The project is queued and waiting to start.
    /// </summary>
    Queued = 0,

    /// <summary>
    /// The project is currently being reviewed.
    /// </summary>
    Reviewing = 1,

    /// <summary>
    /// The project completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// The project ended with a failure.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The project was canceled before completion.
    /// </summary>
    Canceled = 4,
}
