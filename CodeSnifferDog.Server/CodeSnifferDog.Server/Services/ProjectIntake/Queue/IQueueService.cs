namespace CodeSnifferDog.Server.Services.ProjectIntake.Queue;

/// <summary>
/// Persists newly uploaded projects into the processing queue.
/// </summary>
internal interface IQueueService
{
    /// <summary>
    /// Queues one uploaded project request.
    /// </summary>
    /// <param name="request">Queued project request payload.</param>
    /// <param name="cancellationToken">Cancels queue persistence.</param>
    /// <returns>The queued project upload result.</returns>
    Task<ProjectUploadResult> QueueAsync(Request request, CancellationToken cancellationToken);
}
