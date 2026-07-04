namespace CodeSnifferDog.Server.Data.Entities;

/// <summary>
/// Represents the persisted processing state of a project.
/// </summary>
public enum ProjectProcessingStatus
{
    /// <summary>
    /// The project is queued and waiting to start.
    /// </summary>
    Queued = 0,

    /// <summary>
    /// The project is currently under review.
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
