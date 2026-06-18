using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ProjectExecutionCancellationPolicyTests
{
    [TestMethod]
    public void Resolve_ReturnsUserCanceledOutcome_WhenUserRequestedCancellation()
    {
        using CancellationTokenSource hostStoppingTokenSource = new();
        using ProjectExecutionLease lease = new(Guid.NewGuid(), hostStoppingTokenSource.Token, static _ => { });

        bool canceled = lease.TryCancel(ProjectExecutionCancellationSource.UserRequest);
        ProjectExecutionCancellationOutcome outcome = ProjectExecutionCancellationPolicy.Resolve(lease);

        Assert.IsTrue(canceled);
        Assert.AreEqual(ProjectExecutionCancellationSource.UserRequest, lease.CancellationSource);
        Assert.IsTrue(outcome.ShouldUpdateDatabase);
        Assert.IsTrue(outcome.ShouldDeleteUploadedZip);
        Assert.IsTrue(outcome.ShouldDeleteExtractedProject);
    }

    [TestMethod]
    public void Resolve_ReturnsPreserveForRecoveryOutcome_WhenHostStoppingTriggersCancellation()
    {
        using CancellationTokenSource hostStoppingTokenSource = new();
        using ProjectExecutionLease lease = new(Guid.NewGuid(), hostStoppingTokenSource.Token, static _ => { });

        hostStoppingTokenSource.Cancel();
        ProjectExecutionCancellationOutcome outcome = ProjectExecutionCancellationPolicy.Resolve(lease);

        Assert.AreEqual(ProjectExecutionCancellationSource.HostShutdown, lease.CancellationSource);
        Assert.IsFalse(outcome.ShouldUpdateDatabase);
        Assert.IsFalse(outcome.ShouldDeleteUploadedZip);
        Assert.IsFalse(outcome.ShouldDeleteExtractedProject);
    }

    [TestMethod]
    public void HostShutdown_DoesNotOverride_UserRequestedCancellation()
    {
        using CancellationTokenSource hostStoppingTokenSource = new();
        using ProjectExecutionLease lease = new(Guid.NewGuid(), hostStoppingTokenSource.Token, static _ => { });

        bool firstCanceled = lease.TryCancel(ProjectExecutionCancellationSource.UserRequest);
        hostStoppingTokenSource.Cancel();

        Assert.IsTrue(firstCanceled);
        Assert.AreEqual(ProjectExecutionCancellationSource.UserRequest, lease.CancellationSource);
    }
}
