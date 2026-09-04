using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools.Listing;
using CodeSnifferDog.Modules.Tools.Listing;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan.Listing;

/// <summary>
/// Creates bounded project-plan task item pages from stored task items.
/// </summary>
internal static class TaskItemPageFactory
{
    private const int FirstFilePathPreviewLength = 160;

    /// <summary>
    /// Creates one task item page from a page-sized store result that may contain one look-ahead item.
    /// </summary>
    public static TaskItemPage Create(IReadOnlyList<StoredTaskItem> storedTaskItems, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(storedTaskItems);

        bool hasMore = storedTaskItems.Count > pageSize;
        int itemCount = Math.Min(storedTaskItems.Count, pageSize);
        TaskItemListItem[] items = new TaskItemListItem[itemCount];

        for (int index = 0; index < itemCount; index++)
            items[index] = CreateItem(storedTaskItems[index]);

        return new TaskItemPage
        {
            Items = items,
            HasMore = hasMore,
            NextCursor = hasMore
                ? storedTaskItems[itemCount - 1].ProjectPlanTaskItemId
                : null,
        };
    }

    /// <summary>
    /// Creates the compact index representation of one stored task item.
    /// </summary>
    private static TaskItemListItem CreateItem(StoredTaskItem taskItem)
    {
        long totalLines = 0;

        foreach (PlanFile file in taskItem.Files)
            totalLines += file.TotalLines;

        return new TaskItemListItem
        {
            ProjectPlanTaskItemId = taskItem.ProjectPlanTaskItemId,
            FileCount = taskItem.Files.Count,
            TotalLines = totalLines,
            FirstFilePathPreview = TextPreview.Create(taskItem.Files[0].FilePath, FirstFilePathPreviewLength),
        };
    }
}
