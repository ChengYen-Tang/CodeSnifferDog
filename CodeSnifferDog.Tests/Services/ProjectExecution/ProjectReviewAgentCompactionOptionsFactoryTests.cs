using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ProjectReviewAgentCompactionOptionsFactoryTests
{
    [TestMethod]
    public void Create_UsesExecutionContextWindowAndMode_ForEveryWorkflow()
    {
        ProjectReviewAgentCompactionOptionsFactory factory = new();
        ExecutionOptions executionOptions = new()
        {
            MaxParallelAgents = 2,
            ModelContextWindowTokens = 48_000,
            ContextCompactionMode = OperationalContextCompactionMode.ReactiveOnly,
        };

        ProjectReviewAgentCompactionSettings settings = factory.Create(executionOptions);

        AssertCompactionOptions(settings.Scan);
        AssertCompactionOptions(settings.ProjectPlan);
        AssertCompactionOptions(settings.RuleReview);
        AssertCompactionOptions(settings.Report);
    }

    private static void AssertCompactionOptions(OperationalContextCompactionOptions options)
    {
        Assert.AreEqual(48_000L, options.ModelContextWindowTokens);
        Assert.AreEqual(OperationalContextCompactionMode.ReactiveOnly, options.Mode);
    }
}
