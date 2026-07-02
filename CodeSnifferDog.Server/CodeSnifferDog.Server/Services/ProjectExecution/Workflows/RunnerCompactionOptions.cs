using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal static class RunnerCompactionOptions
{
    public static OperationalContextAgentCompactionOptions Create(
        OperationalContextAgentCompactionOptionsFactory factory,
        string summaryPromptAssetPath,
        OperationalContextCompactionOptions options,
        IAgentEventScope eventScope,
        ILoggerFactory loggerFactory) =>
        factory.CreateFromPromptAsset(
            summaryPromptAssetPath,
            options,
            hooks:
            [
                new AgentCompactionEventHook(
                    eventScope,
                    loggerFactory.CreateLogger<AgentCompactionEventHook>()),
            ]);
}
