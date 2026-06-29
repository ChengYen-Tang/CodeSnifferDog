using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Modules.Tools.Scan;
using Microsoft.Extensions.AI;
using System.Reflection;

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
        foreach (AgentFactoryCreateSignature signature in CreateSignatures())
        {
            Assert.AreEqual(
                1,
                signature.FactoryType.GetMethods()
                    .Count(method => method.IsPublic && string.Equals(method.Name, "Create", StringComparison.Ordinal)),
                $"{signature.FactoryType.Name} should expose exactly one public Create method.");
        }
    }

    [TestMethod]
    public void AllImplementedAgentFactories_KeepExactCreateSignatureContract()
    {
        foreach (AgentFactoryCreateSignature signature in CreateSignatures())
        {
            MethodInfo createMethod = signature.FactoryType.GetMethods()
                .Single(method => method.IsPublic && string.Equals(method.Name, "Create", StringComparison.Ordinal));
            ParameterInfo[] parameters = createMethod.GetParameters();

            Assert.AreEqual(typeof(AgentCreationResult), createMethod.ReturnType, $"{signature.FactoryType.Name} return type changed.");
            CollectionAssert.AreEqual(
                signature.ParameterTypes.Select(type => type.FullName).ToArray(),
                parameters.Select(parameter => parameter.ParameterType.FullName).ToArray(),
                $"{signature.FactoryType.Name} parameter types changed.");
            CollectionAssert.AreEqual(
                signature.ParameterNames,
                parameters.Select(parameter => parameter.Name).ToArray(),
                $"{signature.FactoryType.Name} parameter names changed.");

            ParameterInfo eventScopeParameter = parameters[^1];
            Assert.AreEqual("eventScope", eventScopeParameter.Name);
            Assert.AreEqual(typeof(IAgentEventScope), eventScopeParameter.ParameterType);
            Assert.IsTrue(eventScopeParameter.IsOptional, $"{signature.FactoryType.Name} eventScope should stay optional.");
            Assert.IsNull(eventScopeParameter.DefaultValue, $"{signature.FactoryType.Name} eventScope default should stay null.");
        }
    }

    private static IReadOnlyList<AgentFactoryCreateSignature> CreateSignatures() =>
    [
        new(
            typeof(CodeSnifferDog.Agents.Scan.ScanAgentFactory),
            [typeof(IChatClient), typeof(string), typeof(IScanProjectStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "scanProjectStore", "verdictBuffer", "eventScope"]),
        new(
            typeof(CodeSnifferDog.Agents.Scan.ScanVerifierAgentFactory),
            [typeof(IChatClient), typeof(string), typeof(IScanProjectStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "scanProjectStore", "verdictBuffer", "eventScope"]),
        new(
            typeof(CodeSnifferDog.Agents.ProjectPlan.ProjectPlanAgentFactory),
            [typeof(IChatClient), typeof(string), typeof(IProjectPlanTaskItemStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "taskItemStore", "verdictBuffer", "eventScope"]),
        new(
            typeof(CodeSnifferDog.Agents.ProjectPlan.ProjectVerifierAgentFactory),
            [typeof(IChatClient), typeof(string), typeof(StoredScanProject), typeof(IProjectPlanTaskItemStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "scanProject", "taskItemStore", "verdictBuffer", "eventScope"]),
        new(
            typeof(CodeSnifferDog.Agents.RuleReview.RuleReviewAgentFactory),
            [typeof(IChatClient), typeof(string), typeof(string), typeof(string), typeof(StoredProjectPlanTaskItem), typeof(IRuleReviewIssueStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "ruleKey", "ruleMarkdown", "taskItem", "issueStore", "verdictBuffer", "eventScope"]),
        new(
            typeof(CodeSnifferDog.Agents.RuleReview.ReviewVerifierAgentFactory),
            [typeof(IChatClient), typeof(string), typeof(string), typeof(string), typeof(StoredProjectPlanTaskItem), typeof(IRuleReviewIssueStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "ruleKey", "ruleMarkdown", "taskItem", "issueStore", "verdictBuffer", "eventScope"]),
        new(
            typeof(CodeSnifferDog.Agents.Report.ReportAggregatorAgentFactory),
            [typeof(IChatClient), typeof(string), typeof(string), typeof(string), typeof(StoredProjectPlanTaskItem), typeof(IRuleReportIssueStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "ruleKey", "ruleMarkdown", "taskItem", "reportIssueStore", "verdictBuffer", "eventScope"]),
        new(
            typeof(CodeSnifferDog.Agents.Report.ReportVerifierAgentFactory),
            [typeof(IChatClient), typeof(string), typeof(string), typeof(string), typeof(StoredProjectPlanTaskItem), typeof(IReadOnlyList<StoredRuleReviewIssue>), typeof(IRuleReportIssueStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "ruleKey", "ruleMarkdown", "taskItem", "currentFlowIssues", "reportIssueStore", "verdictBuffer", "eventScope"]),
    ];

    private sealed record AgentFactoryCreateSignature(
        Type FactoryType,
        Type[] ParameterTypes,
        string[] ParameterNames);

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
