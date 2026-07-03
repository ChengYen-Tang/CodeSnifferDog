using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Tests.Modules.Tools.ProjectPlan;

[TestClass]
public sealed class InMemoryTaskItemStoreTests
{
    [TestMethod]
    public async Task BeginAttempt_Restore_RewindsStoreState()
    {
        InMemoryTaskItemStore store = new();
        await store.AddAsync(CreateTaskItem("Program.cs"), CancellationToken.None);
        Guid attemptId = Guid.NewGuid();
        IAgentAttemptLease lease = store.BeginAttempt(attemptId);

        await AgentRunAttemptContext.RunAsync(attemptId, async () =>
        {
            await store.AddAsync(CreateTaskItem("Stale.cs"), CancellationToken.None);
            return 0;
        });

        lease.Restore();

        IReadOnlyList<StoredTaskItem> taskItems = await store.ListAsync(CancellationToken.None);
        Assert.HasCount(1, taskItems);
        Assert.AreEqual("Program.cs", taskItems[0].Files[0].FilePath);
    }

    [TestMethod]
    public async Task BeginAttempt_Restore_BlocksLateWritesFromTimedOutAttempt()
    {
        InMemoryTaskItemStore store = new();
        await store.AddAsync(CreateTaskItem("Program.cs"), CancellationToken.None);
        Guid attemptId = Guid.NewGuid();
        IAgentAttemptLease lease = store.BeginAttempt(attemptId);

        lease.Restore();
        StoredTaskItem generatedTaskItem = await AgentRunAttemptContext.RunAsync(attemptId, async () =>
            await store.AddAsync(CreateTaskItem("Late.cs"), CancellationToken.None));

        IReadOnlyList<StoredTaskItem> taskItems = await store.ListAsync(CancellationToken.None);
        Assert.AreEqual("Late.cs", generatedTaskItem.Files[0].FilePath);
        Assert.HasCount(1, taskItems);
        Assert.AreEqual("Program.cs", taskItems[0].Files[0].FilePath);
    }

    [TestMethod]
    public async Task StaleAttempt_DeleteAndClear_DoNotMutate()
    {
        InMemoryTaskItemStore store = new();
        StoredTaskItem taskItem = await store.AddAsync(CreateTaskItem("Program.cs"), CancellationToken.None);
        Guid attemptId = Guid.NewGuid();
        IAgentAttemptLease lease = store.BeginAttempt(attemptId);
        lease.Restore();

        await AgentRunAttemptContext.RunAsync(attemptId, async () =>
        {
            Assert.IsFalse(await store.DeleteAsync(taskItem.ProjectPlanTaskItemId, CancellationToken.None));
            await store.ClearAsync(CancellationToken.None);
            return 0;
        });

        Assert.HasCount(1, await store.ListAsync(CancellationToken.None));
    }

    private static TaskItem CreateTaskItem(string filePath) =>
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
