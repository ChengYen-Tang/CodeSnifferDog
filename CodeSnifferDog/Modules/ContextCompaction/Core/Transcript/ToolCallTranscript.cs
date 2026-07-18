using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Transcript;

/// <summary>
/// Finds transcript boundaries that do not split an assistant function call from its required tool result.
/// </summary>
internal static class ToolCallTranscript
{
    /// <summary>
    /// Returns the longest leading message sequence that satisfies the provider tool-call protocol.
    /// </summary>
    /// <remarks>
    /// A trailing assistant tool-call turn is excluded until every call in that turn has a matching result.
    /// Completed tool-call/result groups remain in the returned sequence so their protocol relationship is preserved.
    /// </remarks>
    public static IReadOnlyList<ChatMessage> GetCompletePrefix(IReadOnlyList<ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        Dictionary<string, int> pendingCalls = [];
        int completeMessageCount = 0;

        for (int messageIndex = 0; messageIndex < messages.Count; messageIndex++)
        {
            ChatMessage message = messages[messageIndex];

            if (pendingCalls.Count > 0)
            {
                if (!IsToolResultMessage(message))
                    return [.. messages.Take(completeMessageCount)];

                foreach (FunctionResultContent functionResult in message.Contents.OfType<FunctionResultContent>())
                {
                    if (!pendingCalls.Remove(functionResult.CallId))
                        return [.. messages.Take(completeMessageCount)];
                }

                if (pendingCalls.Count == 0)
                    completeMessageCount = messageIndex + 1;

                continue;
            }

            if (message.Contents.OfType<FunctionResultContent>().Any() ||
                message.Role == ChatRole.Tool)
                return [.. messages.Take(completeMessageCount)];

            FunctionCallContent[] functionCalls = [.. message.Contents.OfType<FunctionCallContent>()];
            if (functionCalls.Length > 0 && message.Role != ChatRole.Assistant)
                return [.. messages.Take(completeMessageCount)];

            foreach (FunctionCallContent functionCall in functionCalls)
            {
                if (!pendingCalls.TryAdd(functionCall.CallId, messageIndex))
                    return [.. messages.Take(completeMessageCount)];
            }

            if (pendingCalls.Count == 0)
                completeMessageCount = messageIndex + 1;
        }

        return completeMessageCount == messages.Count
            ? messages
            : [.. messages.Take(completeMessageCount)];
    }

    /// <summary>
    /// Determines whether every function call is followed immediately by its matching tool result sequence.
    /// </summary>
    public static bool IsComplete(IReadOnlyList<ChatMessage> messages) =>
        GetCompletePrefix(messages).Count == messages.Count;

    /// <summary>
    /// Moves a proposed suffix start back to the start of every tool-call group it would otherwise split.
    /// </summary>
    /// <param name="messages">Messages from which a suffix will be retained.</param>
    /// <param name="proposedStartIndex">The first message selected by a size- or count-based tail policy.</param>
    /// <returns>A start index that retains whole tool-call/result groups.</returns>
    public static int GetSafeTailStartIndex(
        IReadOnlyList<ChatMessage> messages,
        int proposedStartIndex)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentOutOfRangeException.ThrowIfNegative(proposedStartIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(proposedStartIndex, messages.Count);

        Dictionary<string, int> pendingCalls = [];
        List<ToolCallPair> completedPairs = [];

        for (int messageIndex = 0; messageIndex < messages.Count; messageIndex++)
        {
            foreach (AIContent content in messages[messageIndex].Contents)
            {
                switch (content)
                {
                    case FunctionCallContent functionCall:
                        pendingCalls.TryAdd(functionCall.CallId, messageIndex);
                        break;

                    case FunctionResultContent functionResult when
                        pendingCalls.Remove(functionResult.CallId, out int callMessageIndex):
                        completedPairs.Add(new ToolCallPair(callMessageIndex, messageIndex));
                        break;
                }
            }
        }

        int safeStartIndex = proposedStartIndex;
        bool wasExpanded;

        do
        {
            wasExpanded = false;

            foreach (ToolCallPair pair in completedPairs)
            {
                if (pair.CallMessageIndex < safeStartIndex && pair.ResultMessageIndex >= safeStartIndex)
                {
                    safeStartIndex = pair.CallMessageIndex;
                    wasExpanded = true;
                }
            }

            foreach (int callMessageIndex in pendingCalls.Values)
            {
                if (callMessageIndex < safeStartIndex)
                {
                    safeStartIndex = callMessageIndex;
                    wasExpanded = true;
                }
            }
        }
        while (wasExpanded);

        return safeStartIndex;
    }

    /// <summary>
    /// Associates the messages containing one function call and its corresponding result.
    /// </summary>
    private readonly record struct ToolCallPair(int CallMessageIndex, int ResultMessageIndex);

    /// <summary>
    /// Determines whether a message contains exclusively function-result payloads in the tool role.
    /// </summary>
    private static bool IsToolResultMessage(ChatMessage message) =>
        message.Role == ChatRole.Tool &&
        message.Contents.Count > 0 &&
        message.Contents.All(static content => content is FunctionResultContent);
}
