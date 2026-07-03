using CodeSnifferDog.Server.Client.Components.AgentStatus.State;
using CodeSnifferDog.Server.Client.Components.Reports;
using CodeSnifferDog.Server.Client.Layout.Navigation;
using CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;
using Microsoft.AspNetCore.Components;
using System.Reflection;
using AgentStatusPage = CodeSnifferDog.Server.Client.Pages.AgentStatus;
using HomePage = CodeSnifferDog.Server.Client.Pages.Home;
using ReportsPage = CodeSnifferDog.Server.Client.Pages.Reports;

namespace CodeSnifferDog.Tests.Architecture;

[TestClass]
public sealed class ClientRenderingTests
{
    [TestMethod]
    public void ClientRenderingCollaborators_StayInFocusedNamespaces()
    {
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.AgentStatus.State", typeof(PageState).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.AgentStatus.State", typeof(LiveUpdateReducer).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.AgentStatus.State", typeof(SnapshotState).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.AgentStatus.State", typeof(HistoryState).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.AgentStatus.State", typeof(TimelineEntryList).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.AgentStatus.State", typeof(TimelineMutationResult).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.Reports", typeof(ReportsSidebarPane).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.Reports", typeof(ReportsPreviewPane).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Components.Reports", typeof(ReportFileItemView).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Layout.Navigation", typeof(SidebarProjectionBuilder).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Services.ProjectAgentStatus", typeof(ILiveSubscriptionClient).Namespace);
        Assert.AreEqual("CodeSnifferDog.Server.Client.Services.ProjectAgentStatus", typeof(SignalRLiveSubscriptionClient).Namespace);
    }

    [TestMethod]
    public void ClientRenderingCollaborators_RemainInternalWhereApplicable()
    {
        Type[] internalTypes =
        [
            typeof(PageState),
            typeof(LiveUpdateReducer),
            typeof(SnapshotState),
            typeof(HistoryState),
            typeof(SelectionState),
            typeof(LiveConnectionState),
            typeof(SelectedAgentLiveConnectionState),
            typeof(CompletionState),
            typeof(TimelineEntryList),
            typeof(TimelineMutationResult),
            typeof(SidebarProjectionBuilder),
            typeof(ProjectAction),
            typeof(ProjectActionKind),
            typeof(ProjectItem),
            typeof(ProjectGroup),
        ];

        foreach (Type type in internalTypes)
        {
            Assert.IsFalse(type.IsPublic, $"{type.Name} should remain internal.");
            if (type.Namespace == "CodeSnifferDog.Server.Client.Components.AgentStatus.State")
            {
                Assert.IsFalse(
                    type.Name.StartsWith("AgentStatus", StringComparison.Ordinal),
                    $"{type.Name} should rely on its AgentStatus.State namespace for page context.");
            }
        }
    }

    [TestMethod]
    public void ProjectAgentStatusClientServices_UseLocalRoleNames()
    {
        Type[] projectAgentStatusClientTypes =
        [
            typeof(ILiveSubscriptionClient),
            typeof(SignalRLiveSubscriptionClient),
            typeof(NoOpLiveSubscriptionClient),
        ];

        foreach (Type type in projectAgentStatusClientTypes)
        {
            Assert.IsFalse(
                type.Name.StartsWith("ProjectAgentStatus", StringComparison.Ordinal) ||
                type.Name.StartsWith("IProjectAgentStatus", StringComparison.Ordinal),
                $"{type.Name} should rely on its ProjectAgentStatus namespace for client service context.");
        }
    }

    [TestMethod]
    public void RenderingPages_KeepFocusedBoundaries()
    {
        Assert.IsTrue(HasFieldOfType(typeof(AgentStatusPage), typeof(PageState)));
        Assert.IsTrue(HasFieldOfType(typeof(PageState), typeof(LiveUpdateReducer)));
        Assert.IsTrue(HasFieldOfType(typeof(ReportsPage), typeof(MarkupString)));
        Assert.AreEqual("CodeSnifferDog.Server.Client.Pages", typeof(HomePage).Namespace);

        string reportsSource = ReadClientSource("Pages", "Reports.razor");
        StringAssert.Contains(reportsSource, "<ReportsSidebarPane");
        StringAssert.Contains(reportsSource, "<ReportsPreviewPane");

        string homeSource = ReadClientSource("Pages", "Home.razor");
        StringAssert.Contains(homeSource, "InputFile");
        Assert.DoesNotContain(homeSource, "<ReportsSidebarPane", StringComparison.Ordinal);
        Assert.DoesNotContain(homeSource, "PageState", StringComparison.Ordinal);
    }

    [TestMethod]
    public void NavMenu_DoesNotDeclareProjectionViewModelsInline()
    {
        string navMenuSource = ReadClientSource("Layout", "NavMenu.razor");

        Assert.DoesNotContain(navMenuSource, "private sealed record ProjectAction", StringComparison.Ordinal);
        Assert.DoesNotContain(navMenuSource, "private sealed record ProjectItem", StringComparison.Ordinal);
        Assert.DoesNotContain(navMenuSource, "private sealed class ProjectGroup", StringComparison.Ordinal);
        StringAssert.Contains(navMenuSource, "SidebarProjectionBuilder.CreateGroups");
    }

    [TestMethod]
    public void AgentStatusPage_DoesNotDeclareLargeStateModelsInline()
    {
        string agentStatusSource = ReadClientSource("Pages", "AgentStatus.razor");

        Assert.DoesNotContain(agentStatusSource, "private sealed class PageState", StringComparison.Ordinal);
        Assert.DoesNotContain(agentStatusSource, "private sealed class LiveUpdateReducer", StringComparison.Ordinal);
        Assert.DoesNotContain(agentStatusSource, "private sealed class SnapshotState", StringComparison.Ordinal);
        StringAssert.Contains(agentStatusSource, "PageState.CreateEmpty");
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
