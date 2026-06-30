using CodeSnifferDog.Server.Client.Components.AgentStatus.State;
using CodeSnifferDog.Server.Client.Components.Reports;
using CodeSnifferDog.Server.Client.Layout.Navigation;
using Microsoft.AspNetCore.Components;
using System.Reflection;
using AgentStatusPage = CodeSnifferDog.Server.Client.Pages.AgentStatus;
using HomePage = CodeSnifferDog.Server.Client.Pages.Home;
using ReportsPage = CodeSnifferDog.Server.Client.Pages.Reports;

namespace CodeSnifferDog.Tests.Architecture;

[TestClass]
public sealed class ClientRenderingArchitectureTests
{
    [TestMethod]
    public void ClientRenderingCollaborators_StayInFocusedNamespaces()
    {
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.AgentStatus.State", typeof(AgentStatusPageState).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.AgentStatus.State", typeof(AgentStatusLiveUpdateReducer).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.AgentStatus.State", typeof(AgentStatusSnapshotState).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.AgentStatus.State", typeof(AgentStatusHistoryState).Namespace);
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
            typeof(AgentStatusSnapshotState),
            typeof(AgentStatusHistoryState),
            typeof(AgentStatusSelectionState),
            typeof(AgentStatusLiveConnectionState),
            typeof(AgentStatusSelectedAgentLiveConnectionState),
            typeof(AgentStatusCompletionState),
            typeof(ProjectSidebarProjectionBuilder),
            typeof(ProjectAction),
            typeof(ProjectActionKind),
            typeof(ProjectItem),
            typeof(ProjectGroup),
        ];

        foreach (Type type in internalTypes)
        {
            Assert.IsFalse(type.IsPublic, $"{type.Name} should remain internal.");
        }
    }

    [TestMethod]
    public void RenderingPages_KeepFocusedBoundaries()
    {
        Assert.IsTrue(HasFieldOfType(typeof(AgentStatusPage), typeof(AgentStatusPageState)));
        Assert.IsTrue(HasFieldOfType(typeof(AgentStatusPageState), typeof(AgentStatusLiveUpdateReducer)));
        Assert.IsTrue(HasFieldOfType(typeof(ReportsPage), typeof(MarkupString)));
        Assert.AreEqual("CodeSnifferDog.Server.Client.Pages", typeof(HomePage).Namespace);

        string reportsSource = ReadClientSource("Pages", "Reports.razor");
        StringAssert.Contains(reportsSource, "<ReportsSidebarPane");
        StringAssert.Contains(reportsSource, "<ReportsPreviewPane");

        string homeSource = ReadClientSource("Pages", "Home.razor");
        StringAssert.Contains(homeSource, "InputFile");
        Assert.DoesNotContain(homeSource, "<ReportsSidebarPane", StringComparison.Ordinal);
        Assert.DoesNotContain(homeSource, "AgentStatusPageState", StringComparison.Ordinal);
    }

    [TestMethod]
    public void NavMenu_DoesNotDeclareProjectionViewModelsInline()
    {
        string navMenuSource = ReadClientSource("Layout", "NavMenu.razor");

        Assert.DoesNotContain(navMenuSource, "private sealed record ProjectAction", StringComparison.Ordinal);
        Assert.DoesNotContain(navMenuSource, "private sealed record ProjectItem", StringComparison.Ordinal);
        Assert.DoesNotContain(navMenuSource, "private sealed class ProjectGroup", StringComparison.Ordinal);
        StringAssert.Contains(navMenuSource, "ProjectSidebarProjectionBuilder.CreateGroups");
    }

    [TestMethod]
    public void AgentStatusPage_DoesNotDeclareLargeStateModelsInline()
    {
        string agentStatusSource = ReadClientSource("Pages", "AgentStatus.razor");

        Assert.DoesNotContain(agentStatusSource, "private sealed class AgentStatusPageState", StringComparison.Ordinal);
        Assert.DoesNotContain(agentStatusSource, "private sealed class AgentStatusLiveUpdateReducer", StringComparison.Ordinal);
        Assert.DoesNotContain(agentStatusSource, "private sealed class AgentStatusSnapshotState", StringComparison.Ordinal);
        StringAssert.Contains(agentStatusSource, "AgentStatusPageState.CreateEmpty");
    }

    private static bool HasFieldOfType(Type declaringType, Type fieldType) =>
        declaringType
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Any(field => field.FieldType == fieldType);

    private static string ReadClientSource(params string[] relativeSegments) =>
        File.ReadAllText(Path.Combine(
            [
                GetRepositoryRoot(),
                "CodeSnifferDog.Server",
                "CodeSnifferDog.Server.Client",
                .. relativeSegments,
            ]));

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
