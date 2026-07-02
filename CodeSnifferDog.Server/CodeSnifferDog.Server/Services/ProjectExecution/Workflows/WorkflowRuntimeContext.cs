using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal sealed record WorkflowRuntimeContext(
    IChatClient ChatClient,
    ExecutionOptions ExecutionOptions,
    OperationalContextAgentCompactionOptionsFactory CompactionOptionsFactory,
    PromptAssetReader PromptAssetReader,
    IAgentEventBus AgentEventBus);
