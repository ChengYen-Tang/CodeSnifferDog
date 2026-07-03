using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Modules.Tools.Review.State;

namespace CodeSnifferDog.Tests.Modules.Tools.Review.State;

[TestClass]
public sealed class VerdictStateStoreTests
{
    [TestMethod]
    public void SubmitGetAndReset_UseTrimmedScopeKey()
    {
        ReviewVerdictStateStore store = new();

        store.Submit(" scope ", approved: true, message: "message");
        ReviewVerdict? verdict = store.GetLatest("scope");

        Assert.IsNotNull(verdict);
        Assert.IsTrue(verdict.Approved);
        Assert.AreEqual("message", verdict.Message);

        store.Reset(" scope ");
        Assert.IsNull(store.GetLatest("scope"));
    }

    [TestMethod]
    public void ScopesAreIsolated()
    {
        ReviewVerdictStateStore store = new();

        store.Submit("first", approved: true, message: "first message");
        store.Submit("second", approved: false, message: "second message");

        Assert.IsTrue(store.GetLatest("first")!.Approved);
        Assert.IsFalse(store.GetLatest("second")!.Approved);
    }

    [TestMethod]
    public void CloneRestore_RewindsSingleScope()
    {
        ReviewVerdictStateStore store = new();
        store.Submit("first", approved: true, message: "original");
        store.Submit("second", approved: true, message: "parallel");
        ReviewVerdict? snapshot = store.Clone("first");

        store.Submit("first", approved: false, message: "stale");
        store.Restore("first", snapshot);

        Assert.AreEqual("original", store.GetLatest("first")!.Message);
        Assert.AreEqual("parallel", store.GetLatest("second")!.Message);
    }

    [TestMethod]
    public void Restore_RemovesScope_WhenSnapshotIsNull()
    {
        ReviewVerdictStateStore store = new();
        store.Submit("scope", approved: true, message: "stale");

        store.Restore("scope", snapshot: null);

        Assert.IsNull(store.GetLatest("scope"));
    }
}
