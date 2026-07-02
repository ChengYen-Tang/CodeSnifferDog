using CodeSnifferDog.Models.ReviewAgentTeam;
using FluentResults;

namespace CodeSnifferDog.Workflows.Common;

internal static class WorkflowAgentLifecycle
{
    public static async Task<Result<AgentCreationResult>> CreateAndPublishAsync(
        Func<AgentCreationResult> factory,
        string agentName,
        IAgentEventScope eventScope,
        string displayName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Result<AgentCreationResult> createResult = WorkflowAgentCreation.TryCreate(factory, agentName);

        if (createResult.IsFailed)
            return createResult;

        await eventScope.PublishCreatedAsync(
            displayName,
            createResult.Value.SystemPrompt,
            AgentStatusCatalog.WaitingStatus,
            cancellationToken).ConfigureAwait(false);

        return createResult;
    }
}
