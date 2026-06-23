using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools;
using CodeSnifferDog.Modules.Tools.ProjectPlan;

namespace CodeSnifferDog.Tests.Modules.Tools.ProjectPlan;

[TestClass]
public sealed class ProjectPlanTaskItemToolServiceTests
{
    [TestMethod]
    public async Task AddProjectPlanTaskItemAsync_StoresTrimmedFilesAndReturnsId()
    {
        InMemoryProjectPlanTaskItemStore store = new();
        ProjectPlanTaskItemToolService service = new(store);

        AddProjectPlanTaskItemResult result = await service.AddProjectPlanTaskItemAsync(
            CreateArgs(" Program.cs "),
            CancellationToken.None);

        IReadOnlyList<StoredProjectPlanTaskItem> taskItems = await store.ListAsync(CancellationToken.None);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ProjectPlanTaskItemId));
        Assert.HasCount(1, taskItems);
        Assert.AreEqual(result.ProjectPlanTaskItemId, taskItems[0].ProjectPlanTaskItemId);
        Assert.AreEqual("Program.cs", taskItems[0].Files[0].FilePath);
        Assert.AreEqual(10, taskItems[0].Files[0].TotalLines);
    }

    [TestMethod]
    public async Task AddProjectPlanTaskItemsAsync_StoresBatchAndReturnsIds()
    {
        InMemoryProjectPlanTaskItemStore store = new();
        ProjectPlanTaskItemToolService service = new(store);

        AddProjectPlanTaskItemsResult result = await service.AddProjectPlanTaskItemsAsync(
            new AddProjectPlanTaskItemsArgs
            {
                TaskItems = [CreateArgs("Program.cs"), CreateArgs("Cache.cs")],
            },
            CancellationToken.None);

        Assert.HasCount(2, result.ProjectPlanTaskItemIds);
        Assert.HasCount(2, await store.ListAsync(CancellationToken.None));
    }

    [TestMethod]
    public void AddProjectPlanTaskItemsAsync_Throws_WhenBatchIsEmpty()
    {
        ProjectPlanTaskItemToolService service = new(new InMemoryProjectPlanTaskItemStore());

        Assert.ThrowsExactly<ArgumentException>(() =>
            service.AddProjectPlanTaskItemsAsync(
                new AddProjectPlanTaskItemsArgs
                {
                    TaskItems = [],
                },
                CancellationToken.None).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void AddProjectPlanTaskItemAsync_Throws_WhenFilePathIsEmpty()
    {
        ProjectPlanTaskItemToolService service = new(new InMemoryProjectPlanTaskItemStore());

        Assert.ThrowsExactly<ArgumentException>(() =>
            service.AddProjectPlanTaskItemAsync(CreateArgs(" "), CancellationToken.None).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void AddProjectPlanTaskItemAsync_Throws_WhenTotalLinesIsInvalid()
    {
        ProjectPlanTaskItemToolService service = new(new InMemoryProjectPlanTaskItemStore());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            service.AddProjectPlanTaskItemAsync(CreateArgs("Program.cs", totalLines: 0), CancellationToken.None).GetAwaiter().GetResult());
    }

    [TestMethod]
    public async Task DeleteAndList_UseStore()
    {
        InMemoryProjectPlanTaskItemStore store = new();
        ProjectPlanTaskItemToolService service = new(store);
        AddProjectPlanTaskItemResult result = await service.AddProjectPlanTaskItemAsync(CreateArgs("Program.cs"), CancellationToken.None);

        IReadOnlyList<StoredProjectPlanTaskItem> beforeDelete = await service.ListProjectPlanTaskItemsAsync(CancellationToken.None);
        bool deleted = await service.DeleteProjectPlanTaskItemAsync(
            new DeleteProjectPlanTaskItemArgs
            {
                ProjectPlanTaskItemId = $" {result.ProjectPlanTaskItemId} ",
            },
            CancellationToken.None);

        Assert.HasCount(1, beforeDelete);
        Assert.IsTrue(deleted);
        Assert.IsEmpty(await service.ListProjectPlanTaskItemsAsync(CancellationToken.None));
    }

    private static AddProjectPlanTaskItemArgs CreateArgs(string filePath, int totalLines = 10) =>
        new()
        {
            Files =
            [
                new ProjectPlanFile
                {
                    FilePath = filePath,
                    TotalLines = totalLines,
                },
            ],
        };
}
