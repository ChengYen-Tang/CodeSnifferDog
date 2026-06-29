namespace CodeSnifferDog.Tests.Agents;

[TestClass]
public sealed class AgentFactoryConventionsTests
{
    [TestMethod]
    public void AllImplementedAgentFactories_UseSharedComposerAndBuilderServices()
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

            Assert.Contains("AgentPromptRenderer", source);
            Assert.Contains("AgentToolComposer", source);
            Assert.Contains("AgentBuilderService", source);
            Assert.Contains("_toolComposer.Compose(repositoryRootPath", source);
            Assert.Contains("_agentBuilderService.Create(new AgentBuildRequest(", source);
        }
    }

    [TestMethod]
    public void AllImplementedAgentFactories_KeepSinglePublicCreateSurface()
    {
        Type[] factoryTypes =
        [
            typeof(CodeSnifferDog.Agents.Scan.ScanAgentFactory),
            typeof(CodeSnifferDog.Agents.Scan.ScanVerifierAgentFactory),
            typeof(CodeSnifferDog.Agents.ProjectPlan.ProjectPlanAgentFactory),
            typeof(CodeSnifferDog.Agents.ProjectPlan.ProjectVerifierAgentFactory),
            typeof(CodeSnifferDog.Agents.RuleReview.RuleReviewAgentFactory),
            typeof(CodeSnifferDog.Agents.RuleReview.ReviewVerifierAgentFactory),
            typeof(CodeSnifferDog.Agents.Report.ReportAggregatorAgentFactory),
            typeof(CodeSnifferDog.Agents.Report.ReportVerifierAgentFactory),
        ];

        foreach (Type factoryType in factoryTypes)
        {
            Assert.AreEqual(
                1,
                factoryType.GetMethods()
                    .Count(method => method.IsPublic && string.Equals(method.Name, "Create", StringComparison.Ordinal)),
                $"{factoryType.Name} should expose exactly one public Create method.");
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
