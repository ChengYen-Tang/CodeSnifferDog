using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Models.ContextCompaction.Continuity;

namespace CodeSnifferDog.Tests.Modules.ContextCompaction.Core;

[TestClass]
public sealed class ContinuityStateBuilderTests
{
    [TestMethod]
    public void Build_ParsesOperationalSections()
    {
        ContinuityState state = ContinuityStateBuilder.Build(
            """
            Current objective:
            Review verifier feedback and close remaining gaps.

            Completed work:
            Added staged collapse spans and projection replacement.

            Next steps:
            Rebuild continuity state and rerun tests.

            Critical context:
            Provider usage is telemetry only.

            Open questions:
            Whether collapse should later get a dedicated worker.
            """);

        Assert.AreEqual("Review verifier feedback and close remaining gaps.", state.CurrentObjective);
        Assert.AreEqual("Added staged collapse spans and projection replacement.", state.CompletedWork);
        Assert.AreEqual("Rebuild continuity state and rerun tests.", state.NextSteps);
        Assert.IsTrue(state.CriticalContext.Contains("Provider usage is telemetry only.", StringComparison.Ordinal));
        Assert.IsTrue(state.CriticalContext.Contains("Whether collapse should later get a dedicated worker.", StringComparison.Ordinal));
    }
}
