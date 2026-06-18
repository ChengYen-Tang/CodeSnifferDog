using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ReviewAgentTeam;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal static class RunnerCompactionOptions
{
    public static OperationalContextAgentCompactionOptions Create(
        OperationalContextAgentCompactionOptionsFactory factory,
        string summaryPromptAssetPath,
        OperationalContextCompactionOptions options,
        IAgentEventScope eventScope) =>
        factory.CreateFromPromptAsset(
            summaryPromptAssetPath,
            options,
            hooks:
            [
                new AgentCompactionEventHook(eventScope),
            ]);
}
