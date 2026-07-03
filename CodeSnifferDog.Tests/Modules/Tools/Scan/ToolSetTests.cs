using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.Scan;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Tests.Modules.Tools.Scan;

[TestClass]
public sealed class ToolSetTests
{
    [TestMethod]
    public void CreateScanAgentTools_ReturnsExpectedToolNames()
    {
        ScanToolSet toolSet = new(new InMemoryScanProjectStore(), new ReviewVerdictBuffer());

        IList<AITool> tools = toolSet.CreateScanAgentTools();

        CollectionAssert.AreEqual(
            new[] { "AddScanProject", "AddScanProjects", "DeleteScanProject", "ListScanProjects" },
            tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public void CreateVerifierTools_ReturnsExpectedToolNames()
    {
        ScanToolSet toolSet = new(new InMemoryScanProjectStore(), new ReviewVerdictBuffer());

        IList<AITool> tools = toolSet.CreateVerifierTools();

        CollectionAssert.AreEqual(
            new[] { "ListScanProjects", "SubmitReviewVerdict" },
            tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public async Task PublicMethods_DelegateToServices()
    {
        InMemoryScanProjectStore store = new();
        ReviewVerdictBuffer verdictBuffer = new();
        ScanToolSet toolSet = new(store, verdictBuffer);

        AddScanProjectResult result = await toolSet.AddScanProjectAsync(CreateArgs(" repo "), CancellationToken.None);
        IReadOnlyList<StoredScanProject> projects = await toolSet.ListScanProjectsAsync(CancellationToken.None);
        bool deleted = await toolSet.DeleteScanProjectAsync(
            new DeleteScanProjectArgs
            {
                ScanProjectId = result.ScanProjectId,
            },
            CancellationToken.None);
        bool verdictSubmitted = await toolSet.SubmitReviewVerdictAsync(
            new SubmitReviewVerdictArgs
            {
                Approved = true,
                Message = " approved ",
            },
            CancellationToken.None);

        Assert.HasCount(1, projects);
        Assert.AreEqual("repo", projects[0].ProjectName);
        Assert.IsTrue(deleted);
        Assert.IsTrue(verdictSubmitted);
        Assert.AreEqual("approved", verdictBuffer.Latest!.Message);
    }

    [TestMethod]
    public async Task AddScanProjectsAsync_ReturnsStoredIds()
    {
        ScanToolSet toolSet = new(new InMemoryScanProjectStore(), new ReviewVerdictBuffer());

        AddScanProjectsResult result = await toolSet.AddScanProjectsAsync(
            new AddScanProjectsArgs
            {
                Projects = [CreateArgs("first"), CreateArgs("second")],
            },
            CancellationToken.None);

        Assert.HasCount(2, result.ScanProjectIds);
    }

    private static AddScanProjectArgs CreateArgs(string projectName) =>
        new()
        {
            ProjectName = projectName,
            ProjectPath = @" Z:\repo ",
            ProjectType = " dotnet ",
            Reason = " reason ",
        };
}
