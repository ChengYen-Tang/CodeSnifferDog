using CodeSnifferDog.Agents.Common;
using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewGroup;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry;
using CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;
using CodeSnifferDog.Modules.Tools.Common;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using CodeSnifferDog.Modules.Tools.Scan;
using CodeSnifferDog.Workflows.ProjectPlan;
using CodeSnifferDog.Workflows.Report;
using CodeSnifferDog.Workflows.RuleFlow;
using CodeSnifferDog.Workflows.RuleReview;
using CodeSnifferDog.Workflows.Scan;
using FluentResults;
using Microsoft.Extensions.AI;
using System.Reflection;

namespace CodeSnifferDog.Tests.Architecture;

[TestClass]
public sealed class CoreArchitectureTests
{
    [TestMethod]
    public void WorkflowRunAsyncSurfaces_StayStable()
    {
        foreach (WorkflowRunSignature signature in WorkflowRunSignatures())
        {
            MethodInfo runAsync = signature.WorkflowType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(method => string.Equals(method.Name, "RunAsync", StringComparison.Ordinal));
            ParameterInfo[] parameters = runAsync.GetParameters();

            Assert.AreEqual(signature.ReturnType, runAsync.ReturnType, $"{signature.WorkflowType.Name} return type changed.");
            CollectionAssert.AreEqual(
                signature.ParameterTypes.Select(type => type.FullName).ToArray(),
                parameters.Select(parameter => parameter.ParameterType.FullName).ToArray(),
                $"{signature.WorkflowType.Name} parameter types changed.");
            CollectionAssert.AreEqual(
                signature.ParameterNames,
                parameters.Select(parameter => parameter.Name).ToArray(),
                $"{signature.WorkflowType.Name} parameter names changed.");
        }
    }

    [TestMethod]
    public void WorkflowMessageBuildersAndResultFactories_AreInternalAndDomainLocal()
    {
        Type[] collaboratorTypes =
        [
            typeof(ScanWorkflowMessageBuilder),
            typeof(ScanWorkflowResultFactory),
            typeof(ProjectPlanWorkflowMessageBuilder),
            typeof(ProjectPlanWorkflowResultFactory),
            typeof(RuleReviewWorkflowMessageBuilder),
            typeof(RuleReviewWorkflowResultFactory),
            typeof(RuleReportWorkflowMessageBuilder),
            typeof(RuleReportWorkflowResultFactory),
            typeof(RuleReportDiffService),
        ];

        foreach (Type collaboratorType in collaboratorTypes)
        {
            Assert.IsFalse(collaboratorType.IsPublic, $"{collaboratorType.Name} should remain internal.");
            Assert.StartsWith(
                "CodeSnifferDog.Workflows.",
                collaboratorType.Namespace,
                StringComparison.Ordinal,
                $"{collaboratorType.Name} should stay under a workflow domain namespace.");
        }
    }

    [TestMethod]
    public void Workflows_DoNotExposeToolMetadataOrAgentCompositionDetails()
    {
        Type[] workflowTypes = typeof(ScanWorkflow).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("CodeSnifferDog.Workflows", StringComparison.Ordinal) == true)
            .Where(type => type.Name.EndsWith("Workflow", StringComparison.Ordinal))
            .ToArray();

        foreach (Type workflowType in workflowTypes)
        {
            foreach (MethodInfo method in workflowType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Assert.IsFalse(ContainsType(method.ReturnType, typeof(AITool)), $"{workflowType.Name}.{method.Name} should not expose AITool.");
                Assert.IsFalse(
                    method.GetParameters().Any(parameter => ContainsType(parameter.ParameterType, typeof(AITool))),
                    $"{workflowType.Name}.{method.Name} should not take AITool dependencies.");
            }

            Type[] fieldTypes = workflowType
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .ToArray();

            CollectionAssert.DoesNotContain(fieldTypes, typeof(AgentPromptRenderer), $"{workflowType.Name} should not render agent prompts directly.");
            CollectionAssert.DoesNotContain(fieldTypes, typeof(AgentToolComposer), $"{workflowType.Name} should not compose agent tools directly.");
            CollectionAssert.DoesNotContain(fieldTypes, typeof(AgentBuilderService), $"{workflowType.Name} should not build agents directly.");
        }
    }

    [TestMethod]
    public void Agents_UseSharedCompositionServices()
    {
        foreach (Type factoryType in AgentFactoryTypes())
        {
            Type[] fieldTypes = factoryType
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .ToArray();

            CollectionAssert.Contains(fieldTypes, typeof(AgentPromptRenderer), $"{factoryType.Name} should use AgentPromptRenderer.");
            CollectionAssert.Contains(fieldTypes, typeof(AgentToolComposer), $"{factoryType.Name} should use AgentToolComposer.");
            CollectionAssert.Contains(fieldTypes, typeof(AgentBuilderService), $"{factoryType.Name} should use AgentBuilderService.");
        }
    }

    [TestMethod]
    public void ToolSets_RemainFacadesOverServicesAndFactories()
    {
        foreach (Type toolSetType in ToolSetTypes())
        {
            Type[] fieldTypes = toolSetType
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .ToArray();

            Assert.IsTrue(
                fieldTypes.Any(type => type.Name.EndsWith("ToolService", StringComparison.Ordinal)),
                $"{toolSetType.Name} should delegate orchestration to tool services.");
            Assert.IsFalse(
                fieldTypes.Any(type =>
                    type.Namespace?.Contains(".State", StringComparison.Ordinal) == true ||
                    type.Name.Contains("Store", StringComparison.Ordinal) ||
                    type.Name.Contains("WriteGuard", StringComparison.Ordinal)),
                $"{toolSetType.Name} should not own state store or attempt guard fields.");

            Assert.IsTrue(
                toolSetType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Any(method => method.ReturnType.IsGenericType && method.ReturnType.GetGenericArguments().Any(type => type == typeof(AITool))),
                $"{toolSetType.Name} should remain the public tool metadata facade.");
        }
    }

    [TestMethod]
    public void ContextCompaction_PublicCompatibilityAndInternalNamespaces_StaySeparated()
    {
        Assert.AreEqual("CodeSnifferDog.Modules.ContextCompaction.Core", typeof(OperationalContextMessageShrinker).Namespace);
        Assert.AreEqual("CodeSnifferDog.Modules.ContextCompaction.Core", typeof(OperationalContextChatReducer).Namespace);
        Assert.AreEqual("CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime", typeof(AgentFrameworkCompactionRuntime).Namespace);
        Assert.AreEqual("CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime", typeof(StagedCollapseTracker).Namespace);
        Assert.AreEqual("CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Retry", typeof(ReactiveRetryService).Namespace);
        Assert.AreEqual("CodeSnifferDog.Modules.ContextCompaction.Core.Reduction", typeof(ReductionPipeline).Namespace);
    }

    [TestMethod]
    public void WorkflowMessageBuilders_KeepFocusedMessageConstructionSurface()
    {
        foreach (Type builderType in WorkflowMessageBuilderTypes())
        {
            Type[] fieldTypes = builderType
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.FieldType)
                .ToArray();
            MethodInfo[] publicMethods = builderType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            Assert.AreEqual(1, fieldTypes.Length, $"{builderType.Name} should only hold its workflow message templates.");
            Assert.IsTrue(fieldTypes[0].Name.EndsWith("WorkflowMessageTemplates", StringComparison.Ordinal), $"{builderType.Name} should only depend on workflow message templates.");
            Assert.IsTrue(publicMethods.All(method => method.Name.Contains("Message", StringComparison.Ordinal)), $"{builderType.Name} should expose message construction methods only.");
            Assert.IsTrue(publicMethods.All(method => ContainsType(method.ReturnType, typeof(ChatMessage))), $"{builderType.Name} public methods should return ChatMessage payloads.");
            Assert.IsFalse(
                publicMethods.SelectMany(method => method.GetParameters()).Any(parameter =>
                    IsStoreDependency(parameter.ParameterType) ||
                    parameter.ParameterType == typeof(AgentPromptRenderer) ||
                    parameter.ParameterType == typeof(AgentBuilderService)),
                $"{builderType.Name} should not take store, prompt renderer, or agent lifecycle dependencies.");
        }
    }

    [TestMethod]
    public void WorkflowResultFactories_KeepSingleCreateMappingSurface()
    {
        Type[] factoryTypes =
        [
            typeof(ScanWorkflowResultFactory),
            typeof(ProjectPlanWorkflowResultFactory),
            typeof(RuleReviewWorkflowResultFactory),
            typeof(RuleReportWorkflowResultFactory),
        ];

        foreach (Type factoryType in factoryTypes)
        {
            MethodInfo[] publicMethods = factoryType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);

            Assert.IsTrue(factoryType.IsAbstract && factoryType.IsSealed, $"{factoryType.Name} should stay a static factory.");
            Assert.AreEqual(1, publicMethods.Length, $"{factoryType.Name} should expose only one public mapping method.");
            Assert.AreEqual("Create", publicMethods[0].Name, $"{factoryType.Name} public method should stay Create.");
            Assert.IsTrue(publicMethods[0].ReturnType.Name.EndsWith("WorkflowResult", StringComparison.Ordinal), $"{factoryType.Name}.Create should return a workflow result DTO.");
        }
    }

    private static IReadOnlyList<WorkflowRunSignature> WorkflowRunSignatures() =>
    [
        new(
            typeof(ScanWorkflow),
            typeof(Task<Result<ScanWorkflowResult>>),
            [typeof(string), typeof(CancellationToken)],
            ["repositoryRootPath", "cancellationToken"]),
        new(
            typeof(ProjectPlanWorkflow),
            typeof(Task<Result<ProjectPlanWorkflowResult>>),
            [typeof(string), typeof(StoredScanProject), typeof(CancellationToken)],
            ["repositoryRootPath", "scanProject", "cancellationToken"]),
        new(
            typeof(RuleReviewWorkflow),
            typeof(Task<Result<RuleReviewWorkflowResult>>),
            [typeof(string), typeof(string), typeof(string), typeof(StoredProjectPlanTaskItem), typeof(CancellationToken)],
            ["repositoryRootPath", "ruleKey", "ruleMarkdown", "taskItem", "cancellationToken"]),
        new(
            typeof(RuleReportWorkflow),
            typeof(Task<Result<RuleReportWorkflowResult>>),
            [typeof(string), typeof(string), typeof(string), typeof(StoredProjectPlanTaskItem), typeof(IReadOnlyList<StoredRuleReviewIssue>), typeof(CancellationToken)],
            ["repositoryRootPath", "ruleKey", "ruleMarkdown", "taskItem", "currentFlowIssues", "cancellationToken"]),
        new(
            typeof(RuleFlowWorkflow),
            typeof(Task<Result<RuleFlowWorkflowResult>>),
            [typeof(string), typeof(string), typeof(string), typeof(StoredProjectPlanTaskItem), typeof(CancellationToken)],
            ["repositoryRootPath", "ruleKey", "ruleMarkdown", "taskItem", "cancellationToken"]),
        new(
            typeof(CodeSnifferDog.Workflows.Preparation.RepositoryPreparationWorkflow),
            typeof(Task<Result<RepositoryPreparationWorkflowResult>>),
            [typeof(string), typeof(CancellationToken)],
            ["repositoryRootPath", "cancellationToken"]),
        new(
            typeof(CodeSnifferDog.Workflows.ReviewStage.ReviewStageWorkflow),
            typeof(Task<Result<ReviewStageWorkflowResult>>),
            [typeof(string), typeof(RepositoryPreparationWorkflowResult), typeof(IReadOnlyList<ReviewAgentRuleDefinition>), typeof(CancellationToken)],
            ["repositoryRootPath", "preparationResult", "ruleDefinitions", "cancellationToken"]),
    ];

    private static Type[] AgentFactoryTypes() =>
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

    private static Type[] ToolSetTypes() =>
    [
        typeof(CommonToolSet),
        typeof(ScanToolSet),
        typeof(ProjectPlanToolSet),
        typeof(RuleReviewToolSet),
        typeof(ReportToolSet),
    ];

    private static Type[] WorkflowMessageBuilderTypes() =>
    [
        typeof(ScanWorkflowMessageBuilder),
        typeof(ProjectPlanWorkflowMessageBuilder),
        typeof(RuleReviewWorkflowMessageBuilder),
        typeof(RuleReportWorkflowMessageBuilder),
    ];

    private static bool ContainsType(Type candidate, Type expected)
    {
        if (candidate == expected)
            return true;

        if (candidate.IsGenericType)
            return candidate.GetGenericArguments().Any(argument => ContainsType(argument, expected));

        return false;
    }

    private static bool IsStoreDependency(Type type) =>
        type.Name.EndsWith("Store", StringComparison.Ordinal) &&
        type.Namespace?.StartsWith("CodeSnifferDog.Modules.Tools", StringComparison.Ordinal) == true;

    private sealed record WorkflowRunSignature(
        Type WorkflowType,
        Type ReturnType,
        Type[] ParameterTypes,
        string[] ParameterNames);
}
