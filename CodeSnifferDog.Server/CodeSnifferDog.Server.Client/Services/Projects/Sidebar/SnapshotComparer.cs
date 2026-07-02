using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Server.Client.Services.Projects.Sidebar;

internal static class SnapshotComparer
{
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
