using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Readiness;

internal sealed class ExecutionReadinessGate(IServiceScopeFactory serviceScopeFactory) : IExecutionReadinessGate
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    public ExecutionReadinessResult Check()
    {
        using IServiceScope scope = _serviceScopeFactory.CreateScope();
        IProjectChatClientProvider chatClientProvider = scope.ServiceProvider.GetRequiredService<IProjectChatClientProvider>();
        IReviewRuleMarkdownProvider ruleMarkdownProvider = scope.ServiceProvider.GetRequiredService<IReviewRuleMarkdownProvider>();
        IProjectAnalysisRunner analysisRunner = scope.ServiceProvider.GetRequiredService<IProjectAnalysisRunner>();

        string? reason = null;
        if (!chatClientProvider.IsReady)
        {
            reason =
                "Inference provider is not ready. Configure Inference:Provider and its required ApiKey/ModelId settings.";
        }
        else if (!ruleMarkdownProvider.HasRules)
        {
            reason =
                $"No review rule markdown files were found under '{Path.Combine(AppContext.BaseDirectory, "rules")}'.";
        }

        return new ExecutionReadinessResult(analysisRunner.IsReady, reason);
    }
}
