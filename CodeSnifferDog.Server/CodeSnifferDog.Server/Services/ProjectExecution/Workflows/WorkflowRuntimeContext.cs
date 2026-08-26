using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Workflows.Adapters.AgentFramework.Runtime;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

/// <summary>
/// Carries shared runtime services used by project-execution workflows.
/// </summary>
/// <param name="ChatClient">Chat client used by workflow agents and verifiers.</param>
/// <param name="ExecutionOptions">Execution limits applied to workflow runs.</param>
/// <param name="CompactionOptionsFactory">Factory that creates agent compaction options.</param>
/// <param name="PromptAssetReader">Prompt asset reader used by workflow factories.</param>
/// <param name="AgentEventBus">Event bus that receives workflow agent events.</param>
/// <param name="WorkflowRuntime">Agent Framework runtime that wraps each legacy workflow invocation.</param>
internal sealed record WorkflowRuntimeContext(
    IChatClient ChatClient,
    ExecutionOptions ExecutionOptions,
    AgentOptionsFactory CompactionOptionsFactory,
    PromptAssetReader PromptAssetReader,
    IAgentEventBus AgentEventBus,
    IWorkflowRuntime WorkflowRuntime);
