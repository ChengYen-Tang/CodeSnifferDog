using CodeSnifferDog.Models.ReviewAgentTeam;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CodeSnifferDog.Modules.ReviewAgentTeam;

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
                await eventScope.PublishToolCallStartedAsync(
                    functionCall.CallId,
                    functionCall.Name,
                    SerializePayload(functionCall.Arguments),
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (content is FunctionResultContent functionResult)
            {
                await eventScope.PublishToolCallCompletedAsync(
                    functionResult.CallId,
                    SerializePayload(functionResult.Result),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static string? SerializePayload(object? value) =>
        value switch
        {
            null => null,
            string text => text,
            _ => JsonSerializer.Serialize(value),
        };
}
