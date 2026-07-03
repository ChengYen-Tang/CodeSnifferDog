using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Readiness;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class GateTests
{
    [TestMethod]
    public void Check_WhenChatClientProviderIsNotReady_ReturnsNotReadyReason()
    {
        Gate gate = CreateGate(chatReady: false, hasRules: true, runnerReady: false);

        Result result = gate.Check();

        Assert.IsFalse(result.IsReady);
        StringAssert.Contains(result.Reason!, "Inference provider is not ready");
    }

    [TestMethod]
    public void Check_WhenRulesAreMissing_ReturnsRulesReason()
    {
        Gate gate = CreateGate(chatReady: true, hasRules: false, runnerReady: false);

        Result result = gate.Check();

        Assert.IsFalse(result.IsReady);
        StringAssert.Contains(result.Reason!, "No review rule markdown files were found");
    }

    [TestMethod]
    public void Check_WhenDependenciesAreReady_ReturnsRunnerReadiness()
    {
        Gate gate = CreateGate(chatReady: true, hasRules: true, runnerReady: true);

        Result result = gate.Check();

        Assert.IsTrue(result.IsReady);
        Assert.IsNull(result.Reason);
    }

    private static Gate CreateGate(bool chatReady, bool hasRules, bool runnerReady)
    {
        ServiceCollection services = [];
        services.AddSingleton<IProjectChatClientProvider>(new TestChatClientProvider(chatReady));
        services.AddSingleton<IReviewRuleMarkdownProvider>(new TestRuleMarkdownProvider(hasRules));
        services.AddSingleton<IProjectAnalysisRunner>(new TestAnalysisRunner(runnerReady));
        return new Gate(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    private sealed class TestChatClientProvider(bool isReady) : IProjectChatClientProvider
    {
        public bool IsReady { get; } = isReady;

        public IChatClient CreateChatClient() => throw new NotSupportedException();
    }

    private sealed class TestRuleMarkdownProvider(bool hasRules) : IReviewRuleMarkdownProvider
    {
        public bool HasRules { get; } = hasRules;

        public Task<IReadOnlyList<RuleDefinition>> LoadRulesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestAnalysisRunner(bool isReady) : IProjectAnalysisRunner
    {
        public bool IsReady { get; } = isReady;

        public Task RunAsync(ProjectAnalysisContext context, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
