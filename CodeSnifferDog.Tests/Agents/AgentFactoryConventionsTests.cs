using CodeSnifferDog.Agents.Common;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Modules.Tools.Scan;
using Microsoft.Extensions.AI;
using System.Reflection;
using ReportIssueStore = CodeSnifferDog.Modules.Tools.Report.IIssueStore;
using RuleReviewIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.IIssueStore;

namespace CodeSnifferDog.Tests.Agents;

[TestClass]
public sealed class AgentFactoryConventionsTests
{
    [TestMethod]
    public void AllImplementedAgentFactories_UseSharedComposerAndBuilderServices()
    {
        foreach (AgentFactoryCreateSignature signature in CreateSignatures())
        {
            Type[] fieldTypes = signature.FactoryType
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .ToArray();

            CollectionAssert.Contains(fieldTypes, typeof(AgentPromptRenderer), $"{signature.FactoryType.Name} should use AgentPromptRenderer.");
            CollectionAssert.Contains(fieldTypes, typeof(AgentToolComposer), $"{signature.FactoryType.Name} should use AgentToolComposer.");
            CollectionAssert.Contains(fieldTypes, typeof(AgentBuilderService), $"{signature.FactoryType.Name} should use AgentBuilderService.");
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
            typeof(CodeSnifferDog.Agents.ProjectPlan.AgentFactory),
            [typeof(IChatClient), typeof(string), typeof(ITaskItemStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "taskItemStore", "verdictBuffer", "eventScope"]),
        new(
            typeof(CodeSnifferDog.Agents.ProjectPlan.VerifierFactory),
            [typeof(IChatClient), typeof(string), typeof(StoredScanProject), typeof(ITaskItemStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "scanProject", "taskItemStore", "verdictBuffer", "eventScope"]),
        new(
            typeof(CodeSnifferDog.Agents.RuleReview.AgentFactory),
            [typeof(IChatClient), typeof(string), typeof(string), typeof(string), typeof(StoredTaskItem), typeof(RuleReviewIssueStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "ruleKey", "ruleMarkdown", "taskItem", "issueStore", "verdictBuffer", "eventScope"]),
        new(
            typeof(CodeSnifferDog.Agents.RuleReview.VerifierFactory),
            [typeof(IChatClient), typeof(string), typeof(string), typeof(string), typeof(StoredTaskItem), typeof(RuleReviewIssueStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "ruleKey", "ruleMarkdown", "taskItem", "issueStore", "verdictBuffer", "eventScope"]),
        new(
            typeof(CodeSnifferDog.Agents.Report.ReportAggregatorAgentFactory),
            [typeof(IChatClient), typeof(string), typeof(string), typeof(string), typeof(StoredTaskItem), typeof(ReportIssueStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "ruleKey", "ruleMarkdown", "taskItem", "reportIssueStore", "verdictBuffer", "eventScope"]),
        new(
            typeof(CodeSnifferDog.Agents.Report.ReportVerifierAgentFactory),
            [typeof(IChatClient), typeof(string), typeof(string), typeof(string), typeof(StoredTaskItem), typeof(IReadOnlyList<StoredIssue>), typeof(ReportIssueStore), typeof(ReviewVerdictBuffer), typeof(IAgentEventScope)],
            ["chatClient", "repositoryRootPath", "ruleKey", "ruleMarkdown", "taskItem", "currentFlowIssues", "reportIssueStore", "verdictBuffer", "eventScope"]),
    ];

    private sealed record AgentFactoryCreateSignature(
        Type FactoryType,
        Type[] ParameterTypes,
        string[] ParameterNames);
}
