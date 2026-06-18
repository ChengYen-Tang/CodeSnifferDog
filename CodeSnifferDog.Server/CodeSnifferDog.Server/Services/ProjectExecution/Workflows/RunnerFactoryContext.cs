using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ContextCompaction.Core;
using CodeSnifferDog.Modules.Prompts;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal sealed record RunnerFactoryContext(
    IChatClient ChatClient,
    ExecutionOptions ExecutionOptions,
    OperationalContextAgentCompactionOptionsFactory CompactionOptionsFactory,
    PromptAssetReader PromptAssetReader,
    IAgentEventBus AgentEventBus);
