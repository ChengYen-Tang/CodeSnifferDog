using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Readiness;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ExecutionReadinessGateTests
{
    [TestMethod]
    public void Check_WhenChatClientProviderIsNotReady_ReturnsNotReadyReason()
    {
        ExecutionReadinessGate gate = CreateGate(chatReady: false, hasRules: true, runnerReady: false);

        ExecutionReadinessResult result = gate.Check();

        Assert.IsFalse(result.IsReady);
        StringAssert.Contains(result.Reason!, "Inference provider is not ready");
    }

    [TestMethod]
    public void Check_WhenRulesAreMissing_ReturnsRulesReason()
    {
        ExecutionReadinessGate gate = CreateGate(chatReady: true, hasRules: false, runnerReady: false);

        ExecutionReadinessResult result = gate.Check();

        Assert.IsFalse(result.IsReady);
        StringAssert.Contains(result.Reason!, "No review rule markdown files were found");
    }

    [TestMethod]
    public void Check_WhenDependenciesAreReady_ReturnsRunnerReadiness()
    {
        ExecutionReadinessGate gate = CreateGate(chatReady: true, hasRules: true, runnerReady: true);

        ExecutionReadinessResult result = gate.Check();

        Assert.IsTrue(result.IsReady);
        Assert.IsNull(result.Reason);
    }

    private static ExecutionReadinessGate CreateGate(bool chatReady, bool hasRules, bool runnerReady)
    {
        ServiceCollection services = [];
        services.AddSingleton<IProjectChatClientProvider>(new TestChatClientProvider(chatReady));
        services.AddSingleton<IReviewRuleMarkdownProvider>(new TestRuleMarkdownProvider(hasRules));
        services.AddSingleton<IProjectAnalysisRunner>(new TestAnalysisRunner(runnerReady));
        return new ExecutionReadinessGate(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    private sealed class TestChatClientProvider(bool isReady) : IProjectChatClientProvider
    {
        public bool IsReady { get; } = isReady;

        public IChatClient CreateChatClient() => throw new NotSupportedException();
    }

    private sealed class TestRuleMarkdownProvider(bool hasRules) : IReviewRuleMarkdownProvider
    {
        public bool HasRules { get; } = hasRules;

        public Task<IReadOnlyList<ProjectExecutionRuleDefinition>> LoadRulesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestAnalysisRunner(bool isReady) : IProjectAnalysisRunner
    {
        public bool IsReady { get; } = isReady;

        public Task RunAsync(ProjectAnalysisContext context, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
