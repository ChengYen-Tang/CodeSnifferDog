using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;
using CodeSnifferDog.Models.ContextCompaction.Agents;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

/// <summary>
/// Creates workflow-specific compaction options with the event hooks expected by review runners.
/// </summary>
internal static class RunnerCompactionOptions
{
    /// <summary>
    /// Creates agent compaction options from a prompt asset and a workflow event scope.
    /// </summary>
    /// <param name="factory">Factory that materializes compaction options from prompt assets.</param>
    /// <param name="summaryPromptAssetPath">Prompt asset path used to summarize compacted history.</param>
    /// <param name="options">Compaction settings for the workflow.</param>
    /// <param name="eventScope">Event scope that receives compaction lifecycle notifications.</param>
    /// <param name="loggerFactory">Logger factory used by compaction event hooks.</param>
    /// <returns>The compaction options used by workflow agents.</returns>
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
            ],
            loggerFactory: loggerFactory);
}
