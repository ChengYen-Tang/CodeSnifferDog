using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Tools.Scan.State;

namespace CodeSnifferDog.Tests.Modules.Tools.Scan.State;

[TestClass]
public sealed class ProjectStateStoreTests
{
    [TestMethod]
    public void CreateStoredProject_TrimsFields()
    {
        StoredScanProject project = ScanProjectStateStore.CreateStoredProject(CreateProject(" repo "), "project-id");

        Assert.AreEqual("project-id", project.ScanProjectId);
        Assert.AreEqual("repo", project.ProjectName);
        Assert.AreEqual(@"Z:\repo", project.ProjectPath);
        Assert.AreEqual("dotnet", project.ProjectType);
        Assert.AreEqual("reason", project.Reason);
    }

    [TestMethod]
    public void Add_DeduplicatesEquivalentProjects()
    {
        ScanProjectStateStore store = new();
        StoredScanProject first = ScanProjectStateStore.CreateStoredProject(CreateProject("repo"), "first");
        StoredScanProject duplicate = ScanProjectStateStore.CreateStoredProject(CreateProject("repo"), "duplicate");

        StoredScanProject storedFirst = store.Add(first);
        StoredScanProject storedDuplicate = store.Add(duplicate);

        Assert.AreSame(storedFirst, storedDuplicate);
        Assert.HasCount(1, store.List());
    }

    [TestMethod]
    public void DeleteAndClear_MutateProjectList()
    {
        ScanProjectStateStore store = new();
        StoredScanProject project = store.Add(ScanProjectStateStore.CreateStoredProject(CreateProject("repo"), "project-id"));

        Assert.IsTrue(store.Delete(project.ScanProjectId));
        Assert.IsFalse(store.Delete(project.ScanProjectId));

        store.Add(ScanProjectStateStore.CreateStoredProject(CreateProject("other"), "other-id"));
        store.Clear();

        Assert.IsEmpty(store.List());
    }

    [TestMethod]
    public void CloneRestore_RewindsProjectList()
    {
        ScanProjectStateStore store = new();
        store.Add(ScanProjectStateStore.CreateStoredProject(CreateProject("repo"), "project-id"));
        IReadOnlyList<StoredScanProject> snapshot = store.Clone();

        store.Add(ScanProjectStateStore.CreateStoredProject(CreateProject("stale"), "stale-id"));
        store.Restore(snapshot);

        IReadOnlyList<StoredScanProject> projects = store.List();
        Assert.HasCount(1, projects);
        Assert.AreEqual("repo", projects[0].ProjectName);
    }

    private static ScanProject CreateProject(string projectName) =>
        new()
        {
            ProjectName = projectName,
            ProjectPath = @" Z:\repo ",
            ProjectType = " dotnet ",
            Reason = " reason ",
        };
}
