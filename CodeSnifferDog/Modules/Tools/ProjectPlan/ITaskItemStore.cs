using CodeSnifferDog.Models.ProjectPlan;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

public interface ITaskItemStore : CodeSnifferDog.Workflows.Common.IRetrySafeAgentStore
{
    ValueTask<StoredTaskItem> AddAsync(TaskItem taskItem, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<StoredTaskItem>> AddRangeAsync(
        IReadOnlyList<TaskItem> taskItems,
        CancellationToken cancellationToken);

    ValueTask<bool> DeleteAsync(string projectPlanTaskItemId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<StoredTaskItem>> ListAsync(CancellationToken cancellationToken);

    ValueTask ClearAsync(CancellationToken cancellationToken);
}
