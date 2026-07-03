using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal static class RunnerCompactionOptions
{
    public static AgentCompactionOptions Create(
        AgentOptionsFactory factory,
        string summaryPromptAssetPath,
        CompactionOptions options,
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
