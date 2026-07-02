using CodeSnifferDog.Json;
using CodeSnifferDog.Models.ReviewAgentTeam;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ReviewAgentTeam.Transcript;

internal static class AgentToolEventPublisher
{
    public static async ValueTask PublishAsync(
        ChatMessage message,
        IAgentEventScope eventScope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(eventScope);

        foreach (AIContent content in message.Contents)
        {
            if (content is FunctionCallContent functionCall)
            {
                await PublishStartedAsync(functionCall, eventScope, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (content is FunctionResultContent functionResult)
            {
                await PublishCompletedAsync(functionResult, eventScope, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static ValueTask PublishStartedAsync(
        FunctionCallContent functionCall,
        IAgentEventScope eventScope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(functionCall);
        ArgumentNullException.ThrowIfNull(eventScope);

        return eventScope.PublishToolCallStartedAsync(
            functionCall.CallId,
            functionCall.Name,
            SerializePayload(functionCall.Arguments),
            cancellationToken);
    }

    public static ValueTask PublishCompletedAsync(
        FunctionResultContent functionResult,
        IAgentEventScope eventScope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(functionResult);
        ArgumentNullException.ThrowIfNull(eventScope);

        return eventScope.PublishToolCallCompletedAsync(
            functionResult.CallId,
            SerializePayload(functionResult.Result),
            cancellationToken);
    }

    private static string? SerializePayload(object? value) =>
        value switch
        {
            null => null,
            string text => text,
            _ => CodeSnifferDogJson.Serialize(value, value.GetType()),
        };
}
