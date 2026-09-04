using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools.Listing;
using CodeSnifferDog.Modules.Tools.Listing;

namespace CodeSnifferDog.Modules.Tools.Scan.Listing;

/// <summary>
/// Creates bounded scan-project pages from stored projects.
/// </summary>
internal static class ProjectPageFactory
{
    private const int ProjectNamePreviewLength = 80;
    private const int ProjectPathPreviewLength = 160;
    private const int ProjectTypePreviewLength = 80;
    private const int ReasonPreviewLength = 240;

    /// <summary>
    /// Creates one project page from a page-sized store result that may contain one look-ahead item.
    /// </summary>
    public static ProjectPage Create(IReadOnlyList<StoredScanProject> storedProjects, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(storedProjects);

        bool hasMore = storedProjects.Count > pageSize;
        int itemCount = Math.Min(storedProjects.Count, pageSize);
        ProjectListItem[] items = new ProjectListItem[itemCount];

        for (int index = 0; index < itemCount; index++)
            items[index] = CreateItem(storedProjects[index]);

        return new ProjectPage
        {
            Items = items,
            HasMore = hasMore,
            NextCursor = hasMore
                ? storedProjects[itemCount - 1].ScanProjectId
                : null,
        };
    }

    /// <summary>
    /// Creates the compact index representation of one stored scan project.
    /// </summary>
    private static ProjectListItem CreateItem(StoredScanProject project) =>
        new()
        {
            ScanProjectId = project.ScanProjectId,
            ProjectNamePreview = TextPreview.Create(project.ProjectName, ProjectNamePreviewLength),
            ProjectPathPreview = TextPreview.Create(project.ProjectPath, ProjectPathPreviewLength),
            ProjectTypePreview = TextPreview.Create(project.ProjectType, ProjectTypePreviewLength),
            ReasonPreview = TextPreview.Create(project.Reason, ReasonPreviewLength),
        };
}
