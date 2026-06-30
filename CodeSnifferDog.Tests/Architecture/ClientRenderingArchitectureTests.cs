using CodeSnifferDog.Server.Client.Components.AgentStatus.State;
using CodeSnifferDog.Server.Client.Components.Reports;
using CodeSnifferDog.Server.Client.Layout.Navigation;
using System.Reflection;

namespace CodeSnifferDog.Tests.Architecture;

[TestClass]
public sealed class ClientRenderingArchitectureTests
{
    [TestMethod]
    public void ClientRenderingCollaborators_StayInFocusedNamespaces()
    {
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.AgentStatus.State", typeof(AgentStatusPageState).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.AgentStatus.State", typeof(AgentStatusLiveUpdateReducer).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.Reports", typeof(ReportsSidebarPane).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.Reports", typeof(ReportsPreviewPane).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.Reports", typeof(ReportFileItemView).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Layout.Navigation", typeof(ProjectSidebarProjectionBuilder).Namespace);
    }

    [TestMethod]
    public void ClientRenderingCollaborators_RemainInternalWhereApplicable()
    {
        Type[] internalTypes =
        [
            typeof(AgentStatusPageState),
            typeof(AgentStatusLiveUpdateReducer),
            typeof(ProjectSidebarProjectionBuilder),
            typeof(ProjectAction),
            typeof(ProjectItem),
            typeof(ProjectGroup),
        ];

        foreach (Type type in internalTypes)
        {
            Assert.IsFalse(type.IsPublic, $"{type.Name} should remain internal.");
        }
    }

    [TestMethod]
    public void NavMenu_DoesNotDeclareProjectionViewModelsInline()
    {
        string navMenuSource = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "CodeSnifferDog.Server",
            "CodeSnifferDog.Server.Client",
            "Layout",
            "NavMenu.razor"));

        Assert.DoesNotContain(navMenuSource, "private sealed record ProjectAction", StringComparison.Ordinal);
        Assert.DoesNotContain(navMenuSource, "private sealed record ProjectItem", StringComparison.Ordinal);
        Assert.DoesNotContain(navMenuSource, "private sealed class ProjectGroup", StringComparison.Ordinal);
        StringAssert.Contains(navMenuSource, "ProjectSidebarProjectionBuilder.CreateGroups");
    }

    private static string GetRepositoryRoot()
    {
        string currentDirectory = AppContext.BaseDirectory;
        DirectoryInfo? directory = new(currentDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "CodeSnifferDog.Server")) &&
                Directory.Exists(Path.Combine(directory.FullName, "CodeSnifferDog.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
