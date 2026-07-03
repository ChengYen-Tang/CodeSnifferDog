using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Automatic;
using CodeSnifferDog.Models.ContextCompaction.Collapse;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Continuity;
using CodeSnifferDog.Models.ContextCompaction.Failures;
using CodeSnifferDog.Models.ContextCompaction.Shrinking;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions;
using CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction;

[TestClass]
public sealed class ArchitectureTests
{
    [TestMethod]
    public void CoreCompactionTypes_UseCoreNamespaceAndLocalNames()
    {
        Type[] coreTypes =
        [
            typeof(AgentOptionsFactory),
            typeof(ChatReducer),
            typeof(CollapseController),
            typeof(CollapseProjectionBuilder),
            typeof(CompactionException),
            typeof(ContinuityStateBuilder),
            typeof(MessageShrinker),
        ];

        foreach (Type coreType in coreTypes)
        {
            Assert.AreEqual(
                "CodeSnifferDog.Modules.ContextCompaction.Core",
                coreType.Namespace);
            Assert.IsFalse(
                coreType.Name.StartsWith("OperationalContext", StringComparison.Ordinal),
                $"{coreType.Name} should rely on the ContextCompaction.Core namespace for operational context ownership.");
        }
    }

    [TestMethod]
    public void InternalCollaborators_UseFocusedSubNamespaces()
    {
        Assert.AreEqual(
            "CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime",
            typeof(CompactionRuntime).Namespace);
        Assert.AreEqual(
            "CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime",
            typeof(StagedCollapseTracker).Namespace);
        Assert.AreEqual(
            "CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry",
            typeof(ReactiveRetryService).Namespace);
        Assert.AreEqual(
            "CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions",
            typeof(AutomaticSessionState).Namespace);
        Assert.AreEqual(
            "CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions",
            typeof(CollapseSessionState).Namespace);
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
            typeof(AutomaticSessionState),
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

    [TestMethod]
    public void SessionStateTypes_UseSessionsNamespaceAndLocalNames()
    {
        Type[] sessionStateTypes =
        [
            typeof(AutomaticSessionState),
            typeof(CollapseSessionState),
        ];

        foreach (Type sessionStateType in sessionStateTypes)
        {
            Assert.AreEqual(
                "CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Sessions",
                sessionStateType.Namespace);
            Assert.IsFalse(
                sessionStateType.Name.StartsWith("OperationalContext", StringComparison.Ordinal),
                $"{sessionStateType.Name} should rely on its Sessions namespace for operational context ownership.");
        }
    }

    [TestMethod]
    public void ContextCompactionModels_UseRoleBasedSubNamespacesAndLocalNames()
    {
        (Type Type, string Namespace)[] modelTypes =
        [
            (typeof(AgentCompactionOptions), "CodeSnifferDog.Models.ContextCompaction.Agents"),
            (typeof(AutomaticCompactionState), "CodeSnifferDog.Models.ContextCompaction.Automatic"),
            (typeof(CollapseSnapshot), "CodeSnifferDog.Models.ContextCompaction.Collapse"),
            (typeof(CollapseSpan), "CodeSnifferDog.Models.ContextCompaction.Collapse"),
            (typeof(CollapseState), "CodeSnifferDog.Models.ContextCompaction.Collapse"),
            (typeof(CommittedCollapseSpan), "CodeSnifferDog.Models.ContextCompaction.Collapse"),
            (typeof(StagedCollapseSpan), "CodeSnifferDog.Models.ContextCompaction.Collapse"),
            (typeof(CompactionArtifactMetadata), "CodeSnifferDog.Models.ContextCompaction.Compaction"),
            (typeof(CompactionArtifacts), "CodeSnifferDog.Models.ContextCompaction.Compaction"),
            (typeof(CompactionMessageReference), "CodeSnifferDog.Models.ContextCompaction.Compaction"),
            (typeof(CompactionMode), "CodeSnifferDog.Models.ContextCompaction.Compaction"),
            (typeof(CompactionOptions), "CodeSnifferDog.Models.ContextCompaction.Compaction"),
            (typeof(CompactionReason), "CodeSnifferDog.Models.ContextCompaction.Compaction"),
            (typeof(CompactionResult), "CodeSnifferDog.Models.ContextCompaction.Compaction"),
            (typeof(ContinuityState), "CodeSnifferDog.Models.ContextCompaction.Continuity"),
            (typeof(ModelInvocationFailureKind), "CodeSnifferDog.Models.ContextCompaction.Failures"),
            (typeof(MessageShrinkResult), "CodeSnifferDog.Models.ContextCompaction.Shrinking"),
        ];

        foreach ((Type modelType, string expectedNamespace) in modelTypes)
        {
            Assert.AreEqual(expectedNamespace, modelType.Namespace);
            Assert.IsFalse(
                modelType.Name.StartsWith("OperationalContext", StringComparison.Ordinal),
                $"{modelType.Name} should rely on its role-based namespace for operational context ownership.");
        }
    }
}
