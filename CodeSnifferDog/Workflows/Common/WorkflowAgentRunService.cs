using CodeSnifferDog.Models.ReviewAgentTeam;
using FluentResults;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Workflows.Common;

internal static class WorkflowAgentRunService
{
    public static async Task<(Result Result, int PublishedMessageCount, AIAgent Agent)> RunAsync<TAttemptState>(
        AIAgent agent,
        Func<AIAgent> agentFactory,
        Func<Guid, TAttemptState> prepareAttempt,
        Action<TAttemptState> restoreAttempt,
        List<ChatMessage> messages,
        IAgentEventScope eventScope,
        int publishedMessageCount,
        TimeSpan timeout,
        int maxConsecutiveFailures,
        CancellationToken cancellationToken)
    {
        await eventScope.PublishStatusChangedAsync(AgentStatusCatalog.RunningStatus, cancellationToken)
            .ConfigureAwait(false);

        (Result result, int updatedPublishedMessageCount, AIAgent updatedAgent) = await AgentRunGuard.RunAsync(
            agent,
            agentFactory,
            prepareAttempt,
            restoreAttempt,
            messages,
            eventScope,
            publishedMessageCount,
            timeout,
            maxConsecutiveFailures,
            cancellationToken).ConfigureAwait(false);

        await eventScope.PublishStatusChangedAsync(
            result.IsFailed ? AgentStatusCatalog.DegradedStatus : AgentStatusCatalog.CompletedStatus,
            cancellationToken).ConfigureAwait(false);

        return (result, updatedPublishedMessageCount, updatedAgent);
    }
}
