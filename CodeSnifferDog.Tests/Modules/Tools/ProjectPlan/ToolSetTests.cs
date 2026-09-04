using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools;
using CodeSnifferDog.Models.ProjectPlan.Tools.Listing;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CodeSnifferDog.Tests.Modules.Tools.ProjectPlan;

[TestClass]
public sealed class ToolSetTests
{
    [TestMethod]
    public void CreateProjectPlanAgentTools_ReturnsExpectedToolNames()
    {
        ToolSet toolSet = new(new InMemoryTaskItemStore(), new ReviewVerdictBuffer());

        IList<AITool> tools = toolSet.CreateProjectPlanAgentTools();

        CollectionAssert.AreEqual(
            new[] { "AddProjectPlanTaskItem", "AddProjectPlanTaskItems", "DeleteProjectPlanTaskItem", "ListProjectPlanTaskItems", "ListProjectPlanTaskItemFiles" },
            tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public void CreateVerifierTools_ReturnsExpectedToolNames()
    {
        ToolSet toolSet = new(new InMemoryTaskItemStore(), new ReviewVerdictBuffer());

        IList<AITool> tools = toolSet.CreateVerifierTools();

        CollectionAssert.AreEqual(
            new[] { "ListProjectPlanTaskItems", "ListProjectPlanTaskItemFiles", "SubmitReviewVerdict" },
            tools.Select(tool => tool.Name).ToArray());
    }

    [TestMethod]
    public async Task PublicMethods_DelegateToServices()
    {
        InMemoryTaskItemStore store = new();
        ReviewVerdictBuffer verdictBuffer = new();
        ToolSet toolSet = new(store, verdictBuffer);

        AddProjectPlanTaskItemResult result = await toolSet.AddProjectPlanTaskItemAsync(CreateArgs(" Program.cs "), CancellationToken.None);
        TaskItemPage taskItems = await toolSet.ListProjectPlanTaskItemsAsync(new ListTaskItemsArgs(), CancellationToken.None);
        FilePage files = await toolSet.ListProjectPlanTaskItemFilesAsync(
            new ListFilesArgs
            {
                ProjectPlanTaskItemId = result.ProjectPlanTaskItemId,
            },
            CancellationToken.None);
        bool deleted = await toolSet.DeleteProjectPlanTaskItemAsync(
            new DeleteProjectPlanTaskItemArgs
            {
                ProjectPlanTaskItemId = result.ProjectPlanTaskItemId,
            },
            CancellationToken.None);
        bool verdictSubmitted = await toolSet.SubmitReviewVerdictAsync(
            new SubmitReviewVerdictArgs
            {
                Approved = true,
                Message = " approved ",
            },
            CancellationToken.None);

        Assert.HasCount(1, taskItems.Items);
        Assert.AreEqual("Program.cs", taskItems.Items[0].FirstFilePathPreview);
        Assert.HasCount(1, files.Items);
        Assert.AreEqual("Program.cs", files.Items[0].FilePathPreview);
        Assert.IsTrue(deleted);
        Assert.IsTrue(verdictSubmitted);
        Assert.AreEqual("approved", verdictBuffer.Latest!.Message);
    }

    [TestMethod]
    public async Task AddProjectPlanTaskItemsAsync_ReturnsStoredIds()
    {
        ToolSet toolSet = new(new InMemoryTaskItemStore(), new ReviewVerdictBuffer());

        AddProjectPlanTaskItemsResult result = await toolSet.AddProjectPlanTaskItemsAsync(
            new AddProjectPlanTaskItemsArgs
            {
                TaskItems = [CreateArgs("Program.cs"), CreateArgs("Cache.cs")],
            },
            CancellationToken.None);

        Assert.HasCount(2, result.ProjectPlanTaskItemIds);
    }

    [TestMethod]
    public async Task ListProjectPlanTaskItemsAsync_ReturnsBoundedIndexesAndPagesTaskFiles()
    {
        ToolSet toolSet = new(new InMemoryTaskItemStore(), new ReviewVerdictBuffer());
        IReadOnlyList<PlanFile> files = [.. Enumerable.Range(0, 11).Select(index => new PlanFile
        {
            FilePath = new string('P', 300) + $"/File{index:D2}.cs",
            TotalLines = index + 1,
        })];

        AddProjectPlanTaskItemResult detailedTask = await toolSet.AddProjectPlanTaskItemAsync(
            new AddProjectPlanTaskItemArgs
            {
                Files = files,
            },
            CancellationToken.None);

        for (int index = 0; index < 10; index++)
            await toolSet.AddProjectPlanTaskItemAsync(CreateArgs($"Task{index:D2}.cs"), CancellationToken.None);

        TaskItemPage firstTaskPage = await toolSet.ListProjectPlanTaskItemsAsync(new ListTaskItemsArgs(), CancellationToken.None);
        TaskItemPage secondTaskPage = await toolSet.ListProjectPlanTaskItemsAsync(
            new ListTaskItemsArgs
            {
                Cursor = firstTaskPage.NextCursor,
            },
            CancellationToken.None);
        FilePage firstFilePage = await toolSet.ListProjectPlanTaskItemFilesAsync(
            new ListFilesArgs
            {
                ProjectPlanTaskItemId = detailedTask.ProjectPlanTaskItemId,
            },
            CancellationToken.None);
        FilePage secondFilePage = await toolSet.ListProjectPlanTaskItemFilesAsync(
            new ListFilesArgs
            {
                ProjectPlanTaskItemId = detailedTask.ProjectPlanTaskItemId,
                Offset = firstFilePage.NextOffset,
            },
            CancellationToken.None);

        TaskItemListItem detailedIndex = firstTaskPage.Items
            .Concat(secondTaskPage.Items)
            .Single(item => item.ProjectPlanTaskItemId == detailedTask.ProjectPlanTaskItemId);
        Assert.HasCount(TaskItemPage.DefaultPageSize, firstTaskPage.Items);
        Assert.IsTrue(firstTaskPage.HasMore);
        Assert.IsNotNull(firstTaskPage.NextCursor);
        Assert.HasCount(1, secondTaskPage.Items);
        Assert.IsFalse(secondTaskPage.HasMore);
        Assert.AreEqual(11, detailedIndex.FileCount);
        Assert.AreEqual(66L, detailedIndex.TotalLines);
        Assert.AreEqual(160, detailedIndex.FirstFilePathPreview.Length);
        Assert.IsTrue(detailedIndex.FirstFilePathPreview.EndsWith('…'));
        Assert.HasCount(FilePage.DefaultPageSize, firstFilePage.Items);
        Assert.IsTrue(firstFilePage.HasMore);
        Assert.IsNotNull(firstFilePage.NextOffset);
        Assert.HasCount(1, secondFilePage.Items);
        Assert.IsFalse(secondFilePage.HasMore);
    }

    [TestMethod]
    public async Task ListProjectPlanTaskItemsToolAsync_UsesPagedIndexContract()
    {
        ToolSet toolSet = new(new InMemoryTaskItemStore(), new ReviewVerdictBuffer());

        await toolSet.AddProjectPlanTaskItemAsync(CreateArgs("Program.cs"), CancellationToken.None);
        AIFunction tool = Assert.IsInstanceOfType<AIFunction>(
            toolSet.CreateProjectPlanAgentTools().Single(candidate => candidate.Name == "ListProjectPlanTaskItems"));

        JsonElement result = Assert.IsInstanceOfType<JsonElement>(await tool.InvokeAsync(
            new AIFunctionArguments(),
            CancellationToken.None));
        JsonElement item = result.GetProperty("items")[0];

        Assert.IsTrue(item.TryGetProperty("projectPlanTaskItemId", out _));
        Assert.IsTrue(item.TryGetProperty("fileCount", out _));
        Assert.IsTrue(item.TryGetProperty("totalLines", out _));
        Assert.IsTrue(item.TryGetProperty("firstFilePathPreview", out _));
        Assert.IsFalse(item.TryGetProperty("files", out _));
    }

    private static AddProjectPlanTaskItemArgs CreateArgs(string filePath) =>
        new()
        {
            Files =
            [
                new PlanFile
                {
                    FilePath = filePath,
                    TotalLines = 10,
                },
            ],
        };
}
