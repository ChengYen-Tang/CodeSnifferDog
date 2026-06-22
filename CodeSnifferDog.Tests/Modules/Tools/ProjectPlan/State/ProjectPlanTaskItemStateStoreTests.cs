using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Modules.Tools.ProjectPlan.State;

namespace CodeSnifferDog.Tests.Modules.Tools.ProjectPlan.State;

[TestClass]
public sealed class ProjectPlanTaskItemStateStoreTests
{
    [TestMethod]
    public void CreateStoredTaskItem_TrimsFilePaths()
    {
        StoredProjectPlanTaskItem taskItem = ProjectPlanTaskItemStateStore.CreateStoredTaskItem(CreateTaskItem(" Program.cs "), "task-id");

        Assert.AreEqual("task-id", taskItem.ProjectPlanTaskItemId);
        Assert.AreEqual("Program.cs", taskItem.Files[0].FilePath);
        Assert.AreEqual(10, taskItem.Files[0].TotalLines);
    }

    [TestMethod]
    public void Add_DeduplicatesEquivalentFiles_InOrder()
    {
        ProjectPlanTaskItemStateStore store = new();
        StoredProjectPlanTaskItem first = ProjectPlanTaskItemStateStore.CreateStoredTaskItem(
            CreateTaskItem("Program.cs", "Cache.cs"),
            "first");
        StoredProjectPlanTaskItem duplicate = ProjectPlanTaskItemStateStore.CreateStoredTaskItem(
            CreateTaskItem("Program.cs", "Cache.cs"),
            "duplicate");

        StoredProjectPlanTaskItem storedFirst = store.Add(first);
        StoredProjectPlanTaskItem storedDuplicate = store.Add(duplicate);

        Assert.AreSame(storedFirst, storedDuplicate);
        Assert.HasCount(1, store.List());
    }

    [TestMethod]
    public void Add_TreatsDifferentFileOrderAsDifferentTaskItem()
    {
        ProjectPlanTaskItemStateStore store = new();

        store.Add(ProjectPlanTaskItemStateStore.CreateStoredTaskItem(CreateTaskItem("Program.cs", "Cache.cs"), "first"));
        store.Add(ProjectPlanTaskItemStateStore.CreateStoredTaskItem(CreateTaskItem("Cache.cs", "Program.cs"), "second"));

        Assert.HasCount(2, store.List());
    }

    [TestMethod]
    public void DeleteClearAndCloneRestore_MutateTaskItems()
    {
        ProjectPlanTaskItemStateStore store = new();
        StoredProjectPlanTaskItem taskItem = store.Add(ProjectPlanTaskItemStateStore.CreateStoredTaskItem(CreateTaskItem("Program.cs"), "task-id"));
        IReadOnlyList<StoredProjectPlanTaskItem> snapshot = store.Clone();

        Assert.IsTrue(store.Delete(taskItem.ProjectPlanTaskItemId));
        Assert.IsFalse(store.Delete(taskItem.ProjectPlanTaskItemId));

        store.Restore(snapshot);
        Assert.HasCount(1, store.List());

        store.Clear();
        Assert.IsEmpty(store.List());
    }

    private static ProjectPlanTaskItem CreateTaskItem(params string[] filePaths) =>
        new()
        {
            Files = [.. filePaths.Select(filePath => new ProjectPlanFile
            {
                FilePath = filePath,
                TotalLines = 10,
            })],
        };
}
