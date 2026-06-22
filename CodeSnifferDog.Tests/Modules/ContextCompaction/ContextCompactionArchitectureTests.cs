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
            "CodeSnifferDog.Modules.ContextCompaction.Core.Reduction",
            typeof(ReductionPipeline).Namespace);
    }
}
