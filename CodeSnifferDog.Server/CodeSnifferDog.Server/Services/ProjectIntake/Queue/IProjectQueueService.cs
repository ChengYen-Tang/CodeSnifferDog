namespace CodeSnifferDog.Server.Services.ProjectIntake.Queue;

internal interface IProjectQueueService
{
    Task<ProjectUploadResult> QueueAsync(ProjectQueueRequest request, CancellationToken cancellationToken);
}
