using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class PolicyTests
{
    [TestMethod]
    public void Resolve_ReturnsUserCanceledOutcome_WhenUserRequestedCancellation()
    {
        using CancellationTokenSource hostStoppingTokenSource = new();
        using Lease lease = new(Guid.CreateVersion7(), hostStoppingTokenSource.Token, static _ => { });

        bool canceled = lease.TryCancel(Source.UserRequest);
        Outcome outcome = Policy.Resolve(lease);

        Assert.IsTrue(canceled);
        Assert.AreEqual(Source.UserRequest, lease.CancellationSource);
        Assert.IsTrue(outcome.ShouldUpdateDatabase);
        Assert.IsTrue(outcome.ShouldDeleteUploadedZip);
        Assert.IsTrue(outcome.ShouldDeleteExtractedProject);
    }

    [TestMethod]
    public void Resolve_ReturnsPreserveForRecoveryOutcome_WhenHostStoppingTriggersCancellation()
    {
        using CancellationTokenSource hostStoppingTokenSource = new();
        using Lease lease = new(Guid.CreateVersion7(), hostStoppingTokenSource.Token, static _ => { });

        hostStoppingTokenSource.Cancel();
        Outcome outcome = Policy.Resolve(lease);

        Assert.AreEqual(Source.HostShutdown, lease.CancellationSource);
        Assert.IsFalse(outcome.ShouldUpdateDatabase);
        Assert.IsFalse(outcome.ShouldDeleteUploadedZip);
        Assert.IsFalse(outcome.ShouldDeleteExtractedProject);
    }

    [TestMethod]
    public void HostShutdown_DoesNotOverride_UserRequestedCancellation()
    {
        using CancellationTokenSource hostStoppingTokenSource = new();
        using Lease lease = new(Guid.CreateVersion7(), hostStoppingTokenSource.Token, static _ => { });

        bool firstCanceled = lease.TryCancel(Source.UserRequest);
        hostStoppingTokenSource.Cancel();

        Assert.IsTrue(firstCanceled);
        Assert.AreEqual(Source.UserRequest, lease.CancellationSource);
    }
}
