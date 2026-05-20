using CodeSnifferDog.Models.ProjectPlan;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

public interface IProjectPlanTaskItemStore : CodeSnifferDog.Workflows.Common.IRetrySafeAgentStore
{
    ValueTask<StoredProjectPlanTaskItem> AddAsync(ProjectPlanTaskItem taskItem, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<StoredProjectPlanTaskItem>> AddRangeAsync(
        IReadOnlyList<ProjectPlanTaskItem> taskItems,
        CancellationToken cancellationToken);

    ValueTask<bool> DeleteAsync(string projectPlanTaskItemId, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<StoredProjectPlanTaskItem>> ListAsync(CancellationToken cancellationToken);

    ValueTask ClearAsync(CancellationToken cancellationToken);
}
