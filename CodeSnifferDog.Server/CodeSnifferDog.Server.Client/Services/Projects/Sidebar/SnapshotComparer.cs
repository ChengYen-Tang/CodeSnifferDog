using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Client.Services.Projects.Sidebar;

/// <summary>
/// Compares sidebar snapshots by the state that is actually visible to the UI.
/// </summary>
internal static class SnapshotComparer
{
    /// <summary>
    /// Determines whether two sidebar snapshots are equivalent from the UI's perspective.
    /// </summary>
    /// <param name="left">First sidebar snapshot.</param>
    /// <param name="right">Second sidebar snapshot.</param>
    /// <returns><see langword="true" /> when both snapshots expose the same visible group and project state.</returns>
    public static bool HasEquivalentVisibleState(ProjectSidebarSnapshotDto? left, ProjectSidebarSnapshotDto? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        if (left.Groups.Count != right.Groups.Count)
            return false;

        for (int groupIndex = 0; groupIndex < left.Groups.Count; groupIndex++)
        {
            ProjectSidebarGroupDto leftGroup = left.Groups[groupIndex];
            ProjectSidebarGroupDto rightGroup = right.Groups[groupIndex];
            if (leftGroup.GroupKey != rightGroup.GroupKey ||
                leftGroup.DisplayName != rightGroup.DisplayName ||
                leftGroup.Status != rightGroup.Status ||
                leftGroup.SortOrder != rightGroup.SortOrder ||
                leftGroup.Projects.Count != rightGroup.Projects.Count)
            {
                return false;
            }

            for (int projectIndex = 0; projectIndex < leftGroup.Projects.Count; projectIndex++)
            {
                ProjectSidebarProjectDto leftProject = leftGroup.Projects[projectIndex];
                ProjectSidebarProjectDto rightProject = rightGroup.Projects[projectIndex];
                if (leftProject.ProjectId != rightProject.ProjectId ||
                    leftProject.OriginalFileName != rightProject.OriginalFileName ||
                    leftProject.Status != rightProject.Status ||
                    leftProject.CreatedAtUtc != rightProject.CreatedAtUtc ||
                    leftProject.SortOrder != rightProject.SortOrder)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
