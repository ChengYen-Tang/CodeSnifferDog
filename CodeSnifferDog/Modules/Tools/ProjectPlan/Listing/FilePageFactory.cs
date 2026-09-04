using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools.Listing;
using CodeSnifferDog.Modules.Tools.Listing;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan.Listing;

/// <summary>
/// Creates bounded project-plan task file pages from a stored task item.
/// </summary>
internal static class FilePageFactory
{
    private const int FilePathPreviewLength = 160;

    /// <summary>
    /// Creates one bounded file page for the supplied task item.
    /// </summary>
    public static FilePage Create(StoredTaskItem taskItem, int offset, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(taskItem);
        ArgumentOutOfRangeException.ThrowIfLessThan(offset, 0);

        IReadOnlyList<PlanFile> files = taskItem.Files;
        int itemCount = offset >= files.Count
            ? 0
            : Math.Min(pageSize, files.Count - offset);
        FileListItem[] items = new FileListItem[itemCount];

        for (int index = 0; index < itemCount; index++)
        {
            PlanFile file = files[offset + index];
            items[index] = new FileListItem
            {
                FilePathPreview = TextPreview.Create(file.FilePath, FilePathPreviewLength),
                TotalLines = file.TotalLines,
            };
        }

        bool hasMore = itemCount < files.Count - Math.Min(offset, files.Count);

        return new FilePage
        {
            ProjectPlanTaskItemId = taskItem.ProjectPlanTaskItemId,
            Items = items,
            HasMore = hasMore,
            NextOffset = hasMore
                ? offset + itemCount
                : null,
        };
    }
}
