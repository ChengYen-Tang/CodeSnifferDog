namespace CodeSnifferDog.Tests.Agents;

[TestClass]
public sealed class AgentFactoryConventionsTests
{
    [TestMethod]
    public void AllImplementedAgentFactories_UseCommonTools_AndOperationalContextCompaction()
    {
        string repositoryRootPath = GetRepositoryRootPath();
        string[] relativePaths =
        [
            @"CodeSnifferDog\Agents\Scan\ScanAgentFactory.cs",
            @"CodeSnifferDog\Agents\Scan\ScanVerifierAgentFactory.cs",
            @"CodeSnifferDog\Agents\ProjectPlan\ProjectPlanAgentFactory.cs",
            @"CodeSnifferDog\Agents\ProjectPlan\ProjectVerifierAgentFactory.cs",
            @"CodeSnifferDog\Agents\RuleReview\RuleReviewAgentFactory.cs",
            @"CodeSnifferDog\Agents\RuleReview\ReviewVerifierAgentFactory.cs",
            @"CodeSnifferDog\Agents\Report\ReportAggregatorAgentFactory.cs",
            @"CodeSnifferDog\Agents\Report\ReportVerifierAgentFactory.cs",
        ];

        foreach (string relativePath in relativePaths)
        {
            string absolutePath = Path.Combine(repositoryRootPath, relativePath);
            string source = File.ReadAllText(absolutePath);

            Assert.Contains("commonToolSet.CreateTools()", source);
            Assert.Contains(".UseOperationalContextCompaction(_compactionOptions)", source);
            Assert.Contains(".UseAgentTranscriptEventsIfAvailable(eventScope)", source);
        }
    }

    private static string GetRepositoryRootPath()
    {
        string? current = Path.GetFullPath(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current, "CodeSnifferDog")) &&
                Directory.Exists(Path.Combine(current, "CodeSnifferDog.Tests")))
                return current;

            current = Directory.GetParent(current)?.FullName;
        }

        Assert.Fail("Repository root path could not be located.");
        return string.Empty;
    }
}
