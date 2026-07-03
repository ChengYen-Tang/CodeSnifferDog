using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Tests.Services.ProjectExecution.Worker.ReviewTeam.Compaction;

[TestClass]
public sealed class OptionsFactoryTests
{
    [TestMethod]
    public void Create_UsesExecutionContextWindowAndMode_ForEveryWorkflow()
    {
        OptionsFactory factory = new();
        ExecutionOptions executionOptions = new()
        {
            MaxParallelAgents = 2,
            ModelContextWindowTokens = 48_000,
            ContextCompactionMode = CompactionMode.ReactiveOnly,
        };

        Settings settings = factory.Create(executionOptions);

        AssertCompactionOptions(settings.Scan);
        AssertCompactionOptions(settings.ProjectPlan);
        AssertCompactionOptions(settings.RuleReview);
        AssertCompactionOptions(settings.Report);
    }

    private static void AssertCompactionOptions(CompactionOptions options)
    {
        Assert.AreEqual(48_000L, options.ModelContextWindowTokens);
        Assert.AreEqual(CompactionMode.ReactiveOnly, options.Mode);
    }
}
