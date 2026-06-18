using CodeSnifferDog.Server.Services.ProjectExecution.Status;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class AgentStatusPersistenceServiceTests
{
    [TestMethod]
    public void ParseStatus_UnsupportedStatusThrowsOriginalException()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => AgentStatusPersistenceService.ParseStatus("Paused"));

        Assert.AreEqual("Unsupported agent status 'Paused'.", exception.Message);
    }
}
