using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction;

[TestClass]
public sealed class ContextCompactionArchitectureTests
{
    [TestMethod]
    public void PublicCompactionTypes_RetainCompatibilityNamespaces()
    {
        Assert.AreEqual(
            "CodeSnifferDog.Modules.ContextCompaction.Core",
            typeof(OperationalContextMessageShrinker).Namespace);
        Assert.AreEqual(
            "CodeSnifferDog.Modules.ContextCompaction.Core",
            typeof(OperationalContextChatReducer).Namespace);
    }

    [TestMethod]
    public void InternalCollaborators_UseFocusedSubNamespaces()
    {
        Assert.AreEqual(
            "CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime",
            typeof(AgentFrameworkCompactionRuntime).Namespace);
        Assert.AreEqual(
            "CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime",
            typeof(StagedCollapseTracker).Namespace);
        Assert.AreEqual(
            "CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry",
            typeof(ReactiveRetryService).Namespace);
        Assert.AreEqual(
            "CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions",
            typeof(AutomaticCompactionSessionState).Namespace);
        Assert.AreEqual(
            "CodeSnifferDog.Modules.ContextCompaction.Core.Estimation",
            typeof(TokenEstimator).Namespace);
        Assert.AreEqual(
            "CodeSnifferDog.Modules.ContextCompaction.Core.Reduction",
            typeof(ReductionPipeline).Namespace);
    }

    [TestMethod]
    public void InternalContextCompactionCollaborators_DoNotRepeatNamespaceContext()
    {
        Type[] localCollaborators =
        [
            typeof(AutomaticCompactionSessionState),
            typeof(TokenEstimator),
        ];

        foreach (Type collaborator in localCollaborators)
        {
            Assert.IsFalse(collaborator.IsPublic, $"{collaborator.Name} should remain internal.");
            Assert.IsFalse(
                collaborator.Name.StartsWith("OperationalContext", StringComparison.Ordinal),
                $"{collaborator.Name} should rely on its namespace for operational context ownership.");
        }
    }
}
