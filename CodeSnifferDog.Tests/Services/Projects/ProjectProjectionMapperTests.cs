using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Tests.Services.Projects;

[TestClass]
public sealed class ProjectProjectionMapperTests
{
    [TestMethod]
    public void MapStatus_MapsAllProjectStatuses()
    {
        ProjectProjectionMapper mapper = CreateMapper();

        Assert.AreEqual(ProjectStatus.Queued, mapper.MapStatus(ProjectProcessingStatus.Queued));
        Assert.AreEqual(ProjectStatus.Reviewing, mapper.MapStatus(ProjectProcessingStatus.Reviewing));
        Assert.AreEqual(ProjectStatus.Completed, mapper.MapStatus(ProjectProcessingStatus.Completed));
        Assert.AreEqual(ProjectStatus.Failed, mapper.MapStatus(ProjectProcessingStatus.Failed));
        Assert.AreEqual(ProjectStatus.Canceled, mapper.MapStatus(ProjectProcessingStatus.Canceled));
    }

    [TestMethod]
    public void MapStatus_UnsupportedStatusThrowsOriginalException()
    {
        ProjectProjectionMapper mapper = CreateMapper();

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => mapper.MapStatus((ProjectProcessingStatus)999));

        StringAssert.Contains(exception.Message, "Unsupported project status.");
    }

    [TestMethod]
    public void MapSummary_MapsProjectFields()
    {
        ProjectProjectionMapper mapper = CreateMapper();
        ProjectSummaryProjection project = new(
            Guid.NewGuid(),
            "repo.zip",
            ProjectProcessingStatus.Failed,
            FileSizeBytes: 123,
            CreatedAtUtc: new DateTimeOffset(2026, 5, 15, 8, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc: new DateTimeOffset(2026, 5, 15, 8, 30, 0, TimeSpan.Zero),
            QueueTimestampUtc: new DateTimeOffset(2026, 5, 15, 8, 10, 0, TimeSpan.Zero),
            ProcessingStartedAtUtc: new DateTimeOffset(2026, 5, 15, 9, 0, 0, TimeSpan.Zero),
            FinishedAtUtc: new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero),
            FailureReason: "analysis failed");

        ProjectSummaryDto dto = mapper.MapSummary(project);

        Assert.AreEqual(project.ProjectId, dto.ProjectId);
        Assert.AreEqual(project.OriginalFileName, dto.OriginalFileName);
        Assert.AreEqual(ProjectStatus.Failed, dto.Status);
        Assert.AreEqual(project.FileSizeBytes, dto.FileSizeBytes);
        Assert.AreEqual(project.CreatedAtUtc, dto.CreatedAtUtc);
        Assert.AreEqual(project.UpdatedAtUtc, dto.UpdatedAtUtc);
        Assert.AreEqual(project.QueueTimestampUtc, dto.QueueTimestampUtc);
        Assert.AreEqual(project.ProcessingStartedAtUtc, dto.ProcessingStartedAtUtc);
        Assert.AreEqual(project.FinishedAtUtc, dto.FinishedAtUtc);
        Assert.AreEqual(project.FailureReason, dto.FailureReason);
    }

    [TestMethod]
    public void MapListItem_MapsProjectFields()
    {
        ProjectProjectionMapper mapper = CreateMapper();
        ProjectListItemProjection project = new(
            Guid.NewGuid(),
            "repo.zip",
            ProjectProcessingStatus.Completed,
            new DateTimeOffset(2026, 5, 15, 8, 0, 0, TimeSpan.Zero));

        ProjectListItemDto dto = mapper.MapListItem(project);

        Assert.AreEqual(project.ProjectId, dto.ProjectId);
        Assert.AreEqual(project.OriginalFileName, dto.OriginalFileName);
        Assert.AreEqual(ProjectStatus.Completed, dto.Status);
        Assert.AreEqual(project.CreatedAtUtc, dto.CreatedAtUtc);
    }

    [TestMethod]
    public void MapSidebarProject_MapsProjectFieldsAndSortOrder()
    {
        ProjectProjectionMapper mapper = CreateMapper();
        ProjectSidebarProjectProjection project = new(
            Guid.NewGuid(),
            "repo.zip",
            ProjectProcessingStatus.Reviewing,
            new DateTimeOffset(2026, 5, 15, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 15, 8, 10, 0, TimeSpan.Zero),
            null,
            new DateTimeOffset(2026, 5, 15, 8, 30, 0, TimeSpan.Zero));

        ProjectSidebarProjectDto dto = mapper.MapSidebarProject(project, ProjectStatus.Reviewing, sortOrder: 3);

        Assert.AreEqual(project.ProjectId, dto.ProjectId);
        Assert.AreEqual(project.OriginalFileName, dto.OriginalFileName);
        Assert.AreEqual(ProjectStatus.Reviewing, dto.Status);
        Assert.AreEqual(project.CreatedAtUtc, dto.CreatedAtUtc);
        Assert.AreEqual(3, dto.SortOrder);
    }

    private static ProjectProjectionMapper CreateMapper() => new(new ProjectStatusMapper());
}
