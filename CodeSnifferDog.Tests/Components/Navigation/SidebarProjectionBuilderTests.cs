using CodeSnifferDog.Server.Client.Layout.Navigation;
using CodeSnifferDog.Server.Client.Services.Projects.Sidebar;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CodeSnifferDog.Tests.Components.Navigation;

[TestClass]
public sealed class SidebarProjectionBuilderTests
{
    [TestMethod]
    public void CreatesGroupsWithIconsExpansionAndProjectMetadata()
    {
        Guid reviewingProjectId = Guid.Parse("70000000-0000-0000-0000-000000000501");
        Guid completedProjectId = Guid.Parse("70000000-0000-0000-0000-000000000502");
        State state = CreateState(
            reviewingProjectId,
            CreateGroup("reviewing", "Reviewing", ProjectStatus.Reviewing, 0, CreateProject(reviewingProjectId, "repo-review.zip", ProjectStatus.Reviewing, 1)),
            CreateGroup("completed", "Completed", ProjectStatus.Completed, 1, CreateProject(completedProjectId, "repo-done.zip", ProjectStatus.Completed, 2)),
            CreateGroup("failed", "Failed", ProjectStatus.Failed, 2));
        state.Ui.ToggleGroup("completed", ProjectStatus.Completed);

        IReadOnlyList<ProjectGroup> groups = SidebarProjectionBuilder.CreateGroups(state);

        Assert.HasCount(3, groups);
        AssertGroup(groups[0], "reviewing", "Reviewing", ProjectStatus.Reviewing, "R", "group-icon-reviewing", isExpanded: true);
        AssertGroup(groups[1], "completed", "Completed", ProjectStatus.Completed, "C", "group-icon-completed", isExpanded: false);
        AssertGroup(groups[2], "failed", "Failed", ProjectStatus.Failed, "F", "group-icon-failed", isExpanded: false);
        Assert.AreEqual("repo-review.zip", groups[0].Items[0].Name);
        StringAssert.StartsWith(groups[0].Items[0].Meta, "uploaded ");
    }

    [TestMethod]
    public void CreatesExpectedActionsForReviewingCompletedAndDefaultProjects()
    {
        Guid reviewingProjectId = Guid.Parse("70000000-0000-0000-0000-000000000511");
        Guid completedProjectId = Guid.Parse("70000000-0000-0000-0000-000000000512");
        Guid queuedProjectId = Guid.Parse("70000000-0000-0000-0000-000000000513");
        State state = CreateState(
            reviewingProjectId,
            CreateGroup("reviewing", "Reviewing", ProjectStatus.Reviewing, 0, CreateProject(reviewingProjectId, "reviewing.zip", ProjectStatus.Reviewing, 1)),
            CreateGroup("completed", "Completed", ProjectStatus.Completed, 1, CreateProject(completedProjectId, "completed.zip", ProjectStatus.Completed, 2)),
            CreateGroup("queued", "Queued", ProjectStatus.Queued, 2, CreateProject(queuedProjectId, "queued.zip", ProjectStatus.Queued, 3)));

        IReadOnlyList<ProjectGroup> groups = SidebarProjectionBuilder.CreateGroups(state);

        CollectionAssert.AreEqual(
            new[] { ProjectActionKind.Link, ProjectActionKind.Cancel },
            groups[0].Items[0].Actions.Select(action => action.Kind).ToArray());
        CollectionAssert.AreEqual(
            new[] { "S", "X" },
            groups[0].Items[0].Actions.Select(action => action.IconText).ToArray());
        Assert.AreEqual($"/agent-status?projectId={reviewingProjectId}", groups[0].Items[0].Actions[0].Href);

        CollectionAssert.AreEqual(
            new[] { ProjectActionKind.Link, ProjectActionKind.Link, ProjectActionKind.Delete },
            groups[1].Items[0].Actions.Select(action => action.Kind).ToArray());
        CollectionAssert.AreEqual(
            new[] { "S", "R", "D" },
            groups[1].Items[0].Actions.Select(action => action.IconText).ToArray());
        Assert.AreEqual($"/reports/{completedProjectId}", groups[1].Items[0].Actions[1].Href);

        CollectionAssert.AreEqual(
            new[] { ProjectActionKind.Delete },
            groups[2].Items[0].Actions.Select(action => action.Kind).ToArray());
    }

    [TestMethod]
    public void CreatesAgentStatusAndDeleteActionsForCanceledProjects()
    {
        Guid canceledProjectId = Guid.Parse("70000000-0000-0000-0000-000000000514");
        State state = CreateState(
            selectedProjectId: null,
            CreateGroup(
                "canceled",
                "Canceled",
                ProjectStatus.Canceled,
                0,
                CreateProject(canceledProjectId, "canceled.zip", ProjectStatus.Canceled, 1)));

        ProjectItem canceledProject = SidebarProjectionBuilder.CreateGroups(state)[0].Items[0];

        CollectionAssert.AreEqual(
            new[] { ProjectActionKind.Link, ProjectActionKind.Delete },
            canceledProject.Actions.Select(action => action.Kind).ToArray());
        CollectionAssert.AreEqual(
            new[] { "S", "D" },
            canceledProject.Actions.Select(action => action.IconText).ToArray());
        Assert.AreEqual($"/agent-status?projectId={canceledProjectId}", canceledProject.Actions[0].Href);
    }

    [TestMethod]
    public void LargeProjectionPreservesProjectOrderAndCount()
    {
        Guid selectedProjectId = Guid.Parse("70000000-0000-0000-0001-000000000001");
        State state = CreateState(
            selectedProjectId,
            CreateGroup(
                "reviewing",
                "Reviewing",
                ProjectStatus.Reviewing,
                0,
                Enumerable.Range(1, 100)
                    .Select(index => CreateProject(
                        Guid.Parse($"70000000-0000-0000-0001-{index:000000000000}"),
                        $"repo-{index:000}.zip",
                        ProjectStatus.Reviewing,
                        index))
                    .ToArray()));

        IReadOnlyList<ProjectGroup> groups = SidebarProjectionBuilder.CreateGroups(state);

        Assert.HasCount(1, groups);
        Assert.HasCount(100, groups[0].Items);
        Assert.AreEqual("repo-001.zip", groups[0].Items[0].Name);
        Assert.AreEqual("repo-100.zip", groups[0].Items[^1].Name);
    }

    private static State CreateState(Guid? selectedProjectId, params ProjectSidebarGroupDto[] groups)
    {
        State state = State.CreateEmpty();
        state.ApplySnapshot(
            new ProjectSidebarSnapshotDto
            {
                GeneratedAtUtc = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero),
                SelectedProjectId = selectedProjectId,
                Groups = groups,
            },
            selectedProjectIdFromUri: null);
        return state;
    }

    private static ProjectSidebarGroupDto CreateGroup(
        string groupKey,
        string displayName,
        ProjectStatus status,
        int sortOrder,
        params ProjectSidebarProjectDto[] projects) => new()
        {
            GroupKey = groupKey,
            DisplayName = displayName,
            Status = status,
            SortOrder = sortOrder,
            Projects = projects,
        };

    private static ProjectSidebarProjectDto CreateProject(Guid projectId, string name, ProjectStatus status, int sortOrder) => new()
    {
        ProjectId = projectId,
        OriginalFileName = name,
        Status = status,
        CreatedAtUtc = new DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero).AddMinutes(sortOrder),
        SortOrder = sortOrder,
    };

    private static void AssertGroup(
        ProjectGroup group,
        string groupKey,
        string title,
        ProjectStatus status,
        string iconText,
        string iconCssClass,
        bool isExpanded)
    {
        Assert.AreEqual(groupKey, group.GroupKey);
        Assert.AreEqual(title, group.Title);
        Assert.AreEqual(status, group.Status);
        Assert.AreEqual(iconText, group.IconText);
        Assert.AreEqual(iconCssClass, group.IconCssClass);
        Assert.AreEqual(isExpanded, group.IsExpanded);
    }
}
