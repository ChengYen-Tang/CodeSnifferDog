using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Tools.Scan;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Tests.Modules.Tools.Scan;

[TestClass]
public sealed class InMemoryProjectStoreTests
{
    [TestMethod]
    public async Task BeginAttempt_Restore_RewindsStoreState()
    {
        InMemoryScanProjectStore store = new();
        await store.AddAsync(CreateProject("repo"), CancellationToken.None);
        Guid attemptId = Guid.NewGuid();
        IAgentAttemptLease lease = store.BeginAttempt(attemptId);

        await AgentRunAttemptContext.RunAsync(attemptId, async () =>
        {
            await store.AddAsync(CreateProject("stale"), CancellationToken.None);
            return 0;
        });

        lease.Restore();

        IReadOnlyList<StoredScanProject> projects = await store.ListAsync(CancellationToken.None);
        Assert.HasCount(1, projects);
        Assert.AreEqual("repo", projects[0].ProjectName);
    }

    [TestMethod]
    public async Task BeginAttempt_Restore_BlocksLateWritesFromTimedOutAttempt()
    {
        InMemoryScanProjectStore store = new();
        await store.AddAsync(CreateProject("repo"), CancellationToken.None);
        Guid attemptId = Guid.NewGuid();
        IAgentAttemptLease lease = store.BeginAttempt(attemptId);

        lease.Restore();
        StoredScanProject generatedProject = await AgentRunAttemptContext.RunAsync(attemptId, async () =>
            await store.AddAsync(CreateProject("late"), CancellationToken.None));

        IReadOnlyList<StoredScanProject> projects = await store.ListAsync(CancellationToken.None);
        Assert.AreEqual("late", generatedProject.ProjectName);
        Assert.HasCount(1, projects);
        Assert.AreEqual("repo", projects[0].ProjectName);
    }

    [TestMethod]
    public async Task StaleAttempt_DeleteAndClear_DoNotMutate()
    {
        InMemoryScanProjectStore store = new();
        StoredScanProject project = await store.AddAsync(CreateProject("repo"), CancellationToken.None);
        Guid attemptId = Guid.NewGuid();
        IAgentAttemptLease lease = store.BeginAttempt(attemptId);
        lease.Restore();

        await AgentRunAttemptContext.RunAsync(attemptId, async () =>
        {
            Assert.IsFalse(await store.DeleteAsync(project.ScanProjectId, CancellationToken.None));
            await store.ClearAsync(CancellationToken.None);
            return 0;
        });

        Assert.HasCount(1, await store.ListAsync(CancellationToken.None));
    }

    private static ScanProject CreateProject(string projectName) =>
        new()
        {
            ProjectName = projectName,
            ProjectPath = @"Z:\repo",
            ProjectType = "dotnet",
            Reason = "reason",
        };
}
