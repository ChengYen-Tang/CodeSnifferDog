using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Tests.Workflows.Common;

[TestClass]
public sealed class WorkflowAttemptLeasePairTests
{
    [TestMethod]
    public void Restore_RestoresStoreThenVerdictLeaseExactlyOnce()
    {
        List<string> calls = [];
        RecordingLease storeLease = new("store", calls);
        RecordingLease verdictLease = new("verdict", calls);
        WorkflowAttemptLeasePair pair = new(storeLease, verdictLease);

        pair.Restore();

        CollectionAssert.AreEqual(new[] { "store", "verdict" }, calls.ToArray());
        Assert.AreEqual(1, storeLease.RestoreCount);
        Assert.AreEqual(1, verdictLease.RestoreCount);
    }

    private sealed class RecordingLease(string name, List<string> calls) : IAgentAttemptLease
    {
        public int RestoreCount { get; private set; }

        public void Restore()
        {
            RestoreCount++;
            calls.Add(name);
        }
    }
}
