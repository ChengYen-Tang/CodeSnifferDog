using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools;
using CodeSnifferDog.Models.Scan.Tools.Listing;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.Scan;
using Microsoft.Extensions.AI;
using System.Text.Json;

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
        ProjectPage projects = await toolSet.ListScanProjectsAsync(new ListProjectsArgs(), CancellationToken.None);
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

        Assert.HasCount(1, projects.Items);
        Assert.AreEqual("repo", projects.Items[0].ProjectNamePreview);
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

    [TestMethod]
    public async Task ListScanProjectsAsync_ReturnsBoundedIndexesAndContinuation()
    {
        ScanToolSet toolSet = new(new InMemoryScanProjectStore(), new ReviewVerdictBuffer());

        for (int index = 0; index < 11; index++)
        {
            await toolSet.AddScanProjectAsync(
                CreateArgs($"Project{index:D2}", $"src/Project{index:D2}.csproj", "dotnet", new string('R', 300)),
                CancellationToken.None);
        }

        ProjectPage firstPage = await toolSet.ListScanProjectsAsync(new ListProjectsArgs(), CancellationToken.None);
        ProjectPage secondPage = await toolSet.ListScanProjectsAsync(
            new ListProjectsArgs
            {
                Cursor = firstPage.NextCursor,
            },
            CancellationToken.None);

        Assert.HasCount(ProjectPage.DefaultPageSize, firstPage.Items);
        Assert.IsTrue(firstPage.HasMore);
        Assert.IsNotNull(firstPage.NextCursor);
        Assert.HasCount(1, secondPage.Items);
        Assert.IsFalse(secondPage.HasMore);
        Assert.IsNull(secondPage.NextCursor);
        Assert.AreEqual(240, firstPage.Items[0].ReasonPreview.Length);
        Assert.IsTrue(firstPage.Items[0].ReasonPreview.EndsWith('…'));
    }

    [TestMethod]
    public async Task ListScanProjectsToolAsync_UsesPagedIndexContract()
    {
        ScanToolSet toolSet = new(new InMemoryScanProjectStore(), new ReviewVerdictBuffer());

        await toolSet.AddScanProjectAsync(CreateArgs("Core"), CancellationToken.None);
        AIFunction tool = Assert.IsInstanceOfType<AIFunction>(
            toolSet.CreateScanAgentTools().Single(candidate => candidate.Name == "ListScanProjects"));

        JsonElement result = Assert.IsInstanceOfType<JsonElement>(await tool.InvokeAsync(
            new AIFunctionArguments(),
            CancellationToken.None));
        JsonElement item = result.GetProperty("items")[0];

        Assert.IsTrue(item.TryGetProperty("scanProjectId", out _));
        Assert.IsTrue(item.TryGetProperty("projectNamePreview", out _));
        Assert.IsTrue(item.TryGetProperty("projectPathPreview", out _));
        Assert.IsTrue(item.TryGetProperty("projectTypePreview", out _));
        Assert.IsTrue(item.TryGetProperty("reasonPreview", out _));
        Assert.IsFalse(item.TryGetProperty("reason", out _));
    }

    private static AddScanProjectArgs CreateArgs(
        string projectName,
        string projectPath = @" Z:\repo ",
        string projectType = " dotnet ",
        string reason = " reason ") =>
        new()
        {
            ProjectName = projectName,
            ProjectPath = projectPath,
            ProjectType = projectType,
            Reason = reason,
        };
}
