using CodeSnifferDog.Models.ReviewAgentTeam;
using FluentResults;

namespace CodeSnifferDog.Workflows.Common;

internal static class WorkflowAgentCreation
{
    public static Result<AgentCreationResult> TryCreate(Func<AgentCreationResult> factory, string agentName)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        try
        {
            return Result.Ok(factory());
        }
        catch (Exception ex)
        {
            return Result.Fail(new ExceptionalError($"Failed to create {agentName}: {ex}", ex));
        }
    }
}
