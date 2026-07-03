using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;

internal interface IStateService
{
    Task<bool> CanStartExecutionAsync(Guid projectId, CancellationToken cancellationToken);

    Task CompleteAsync(
        Guid projectId,
        ProjectProcessingStatus status,
        string? failureReason,
        CancellationToken cancellationToken);

    Task PublishStatusUpdateAsync(
        Guid projectId,
        ProjectProcessingStatus status,
        CancellationToken cancellationToken);
}
