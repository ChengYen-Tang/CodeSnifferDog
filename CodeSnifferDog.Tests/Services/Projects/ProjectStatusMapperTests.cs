using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Tests.Services.Projects;

[TestClass]
public sealed class ProjectStatusMapperTests
{
    [TestMethod]
    public void Map_MapsAllProjectStatuses()
    {
        ProjectStatusMapper mapper = new();

        Assert.AreEqual(ProjectStatus.Queued, mapper.Map(ProjectProcessingStatus.Queued));
        Assert.AreEqual(ProjectStatus.Reviewing, mapper.Map(ProjectProcessingStatus.Reviewing));
        Assert.AreEqual(ProjectStatus.Completed, mapper.Map(ProjectProcessingStatus.Completed));
        Assert.AreEqual(ProjectStatus.Failed, mapper.Map(ProjectProcessingStatus.Failed));
        Assert.AreEqual(ProjectStatus.Canceled, mapper.Map(ProjectProcessingStatus.Canceled));
    }

    [TestMethod]
    public void Map_SurfaceUnsupportedStatusThrowsOriginalException()
    {
        ProjectStatusMapper mapper = new();

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => mapper.Map((ProjectProcessingStatus)999));

        StringAssert.Contains(exception.Message, "Unsupported project status.");
    }

    [TestMethod]
    public void Map_PersistedUnsupportedStatusThrowsOriginalException()
    {
        ProjectStatusMapper mapper = new();

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => mapper.Map(
                (ProjectProcessingStatus)999,
                ProjectStatusMappingExceptionStyle.Persisted));

        Assert.AreEqual("Unsupported project status '999'.", exception.Message);
    }
}
