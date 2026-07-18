using CodeSnifferDog.Server.Shared.Projects;

namespace CodeSnifferDog.Tests.Shared.Projects;

[TestClass]
public sealed class ProjectSidebarRouteSelectionTests
{
    [TestMethod]
    public void ExtractSelectedProjectId_ReturnsExpectedSelection()
    {
        (string UriText, string RelativePath, string? ExpectedProjectId)[] testCases =
        [
            ("http://localhost/reports/80000000-0000-0000-0000-000000000001", "reports/80000000-0000-0000-0000-000000000001", "80000000-0000-0000-0000-000000000001"),
            ("http://localhost/agent-status?projectId=80000000-0000-0000-0000-000000000002", "agent-status", "80000000-0000-0000-0000-000000000002"),
            ("http://localhost/agent-status?projectId=80000000-0000-0000-0000-000000000002", "agent-status?projectId=80000000-0000-0000-0000-000000000002", "80000000-0000-0000-0000-000000000002"),
            ("http://localhost/agent-status?projectId=not-a-guid", "agent-status", null),
            ("http://localhost/projects", "projects", null),
        ];

        foreach ((string uriText, string relativePath, string? expectedProjectId) in testCases)
        {
            Guid? selectedProjectId = ProjectSidebarRouteSelection.ExtractSelectedProjectId(new Uri(uriText), relativePath);

            Assert.AreEqual(
                expectedProjectId is null ? null : Guid.Parse(expectedProjectId),
                selectedProjectId);
        }
    }
}
