using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Modules.Tools.ProjectPlan.State;

namespace CodeSnifferDog.Tests.Modules.Tools.ProjectPlan.State;

[TestClass]
public sealed class TaskItemStateStoreTests
{
    [TestMethod]
    public void CreateStoredTaskItem_TrimsFilePaths()
    {
        StoredTaskItem taskItem = TaskItemStateStore.CreateStoredTaskItem(CreateTaskItem(" Program.cs "), "task-id");

        Assert.AreEqual("task-id", taskItem.ProjectPlanTaskItemId);
        Assert.AreEqual("Program.cs", taskItem.Files[0].FilePath);
        Assert.AreEqual(10, taskItem.Files[0].TotalLines);
    }

    [TestMethod]
    public void Add_DeduplicatesEquivalentFiles_InOrder()
    {
        TaskItemStateStore store = new();
        StoredTaskItem first = TaskItemStateStore.CreateStoredTaskItem(
            CreateTaskItem("Program.cs", "Cache.cs"),
            "first");
        StoredTaskItem duplicate = TaskItemStateStore.CreateStoredTaskItem(
            CreateTaskItem("Program.cs", "Cache.cs"),
            "duplicate");

        StoredTaskItem storedFirst = store.Add(first);
        StoredTaskItem storedDuplicate = store.Add(duplicate);

        Assert.AreSame(storedFirst, storedDuplicate);
        Assert.HasCount(1, store.ListAll());
    }

    [TestMethod]
    public void Add_TreatsDifferentFileOrderAsDifferentTaskItem()
    {
        TaskItemStateStore store = new();

        store.Add(TaskItemStateStore.CreateStoredTaskItem(CreateTaskItem("Program.cs", "Cache.cs"), "first"));
        store.Add(TaskItemStateStore.CreateStoredTaskItem(CreateTaskItem("Cache.cs", "Program.cs"), "second"));

        Assert.HasCount(2, store.ListAll());
    }

    [TestMethod]
    public void DeleteClearAndCloneRestore_MutateTaskItems()
    {
        TaskItemStateStore store = new();
        StoredTaskItem taskItem = store.Add(TaskItemStateStore.CreateStoredTaskItem(CreateTaskItem("Program.cs"), "task-id"));
        IReadOnlyList<StoredTaskItem> snapshot = store.Clone();

        Assert.IsTrue(store.Delete(taskItem.ProjectPlanTaskItemId));
        Assert.IsFalse(store.Delete(taskItem.ProjectPlanTaskItemId));

        store.Restore(snapshot);
        Assert.HasCount(1, store.ListAll());

        store.Clear();
        Assert.IsEmpty(store.ListAll());
    }

    private static TaskItem CreateTaskItem(params string[] filePaths) =>
        new()
        {
            Files = [.. filePaths.Select(filePath => new PlanFile
            {
                FilePath = filePath,
                TotalLines = 10,
            })],
        };
}
