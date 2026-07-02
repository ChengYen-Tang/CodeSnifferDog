namespace CodeSnifferDog.Server.Services.ProjectIntake.Queue;

internal interface IQueueService
{
    Task<ProjectUploadResult> QueueAsync(Request request, CancellationToken cancellationToken);
}
