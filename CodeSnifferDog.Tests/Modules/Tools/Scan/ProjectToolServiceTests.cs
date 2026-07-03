using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools;
using CodeSnifferDog.Modules.Tools.Scan;

namespace CodeSnifferDog.Tests.Modules.Tools.Scan;

[TestClass]
public sealed class ProjectToolServiceTests
{
    [TestMethod]
    public async Task AddScanProjectAsync_StoresTrimmedProjectAndReturnsId()
    {
        InMemoryScanProjectStore store = new();
        ScanProjectToolService service = new(store);

        AddScanProjectResult result = await service.AddScanProjectAsync(CreateArgs(" repo "), CancellationToken.None);

        IReadOnlyList<StoredScanProject> projects = await store.ListAsync(CancellationToken.None);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ScanProjectId));
        Assert.HasCount(1, projects);
        Assert.AreEqual(result.ScanProjectId, projects[0].ScanProjectId);
        Assert.AreEqual("repo", projects[0].ProjectName);
        Assert.AreEqual(@"Z:\repo", projects[0].ProjectPath);
        Assert.AreEqual("dotnet", projects[0].ProjectType);
        Assert.AreEqual("reason", projects[0].Reason);
    }

    [TestMethod]
    public async Task AddScanProjectsAsync_StoresBatchAndReturnsIds()
    {
        InMemoryScanProjectStore store = new();
        ScanProjectToolService service = new(store);

        AddScanProjectsResult result = await service.AddScanProjectsAsync(
            new AddScanProjectsArgs
            {
                Projects = [CreateArgs("first"), CreateArgs("second")],
            },
            CancellationToken.None);

        Assert.HasCount(2, result.ScanProjectIds);
        Assert.HasCount(2, await store.ListAsync(CancellationToken.None));
    }

    [TestMethod]
    public void AddScanProjectsAsync_Throws_WhenBatchIsEmpty()
    {
        ScanProjectToolService service = new(new InMemoryScanProjectStore());

        Assert.ThrowsExactly<ArgumentException>(() =>
            service.AddScanProjectsAsync(
                new AddScanProjectsArgs
                {
                    Projects = [],
                },
                CancellationToken.None).GetAwaiter().GetResult());
    }

    [TestMethod]
    public void AddScanProjectAsync_Throws_WhenRequiredFieldIsEmpty()
    {
        ScanProjectToolService service = new(new InMemoryScanProjectStore());

        Assert.ThrowsExactly<ArgumentException>(() =>
            service.AddScanProjectAsync(
                new AddScanProjectArgs
                {
                    ProjectName = "repo",
                    ProjectPath = " ",
                    ProjectType = "dotnet",
                    Reason = "reason",
                },
                CancellationToken.None).GetAwaiter().GetResult());
    }

    [TestMethod]
    public async Task DeleteAndList_UseStore()
    {
        InMemoryScanProjectStore store = new();
        ScanProjectToolService service = new(store);
        AddScanProjectResult result = await service.AddScanProjectAsync(CreateArgs("repo"), CancellationToken.None);

        IReadOnlyList<StoredScanProject> beforeDelete = await service.ListScanProjectsAsync(CancellationToken.None);
        bool deleted = await service.DeleteScanProjectAsync(
            new DeleteScanProjectArgs
            {
                ScanProjectId = $" {result.ScanProjectId} ",
            },
            CancellationToken.None);

        Assert.HasCount(1, beforeDelete);
        Assert.IsTrue(deleted);
        Assert.IsEmpty(await service.ListScanProjectsAsync(CancellationToken.None));
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
