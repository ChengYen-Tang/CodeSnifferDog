using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Agents.Common.TokenUsage;

/// <summary>
/// Attaches the stable model identity configured for one chat-client instance.
/// </summary>
internal static class ChatClientIdentity
{
    /// <summary>
    /// Wraps a chat client with a model identity that can be recovered through the chat-client service contract.
    /// </summary>
    /// <param name="chatClient">Chat client whose identity should be exposed.</param>
    /// <param name="modelId">Stable configured model or deployment identifier.</param>
    /// <returns>A chat client that delegates all operations and exposes the configured identity.</returns>
    public static IChatClient Attach(IChatClient chatClient, string modelId)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return new IdentityChatClient(chatClient, new ModelIdentity(modelId.Trim()));
    }

    /// <summary>
    /// Reads the stable configured model identity from a chat-client pipeline.
    /// </summary>
    /// <param name="chatClient">Chat client whose identity should be inspected.</param>
    /// <returns>The configured model identity, or <see langword="null" /> when the pipeline does not expose one.</returns>
    public static string? TryGetModelId(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        return chatClient.GetService(typeof(ModelIdentity)) is ModelIdentity identity
            ? identity.ModelId
            : null;
    }

    private sealed class ModelIdentity(string modelId)
    {
        public string ModelId { get; } = modelId;
    }

    private sealed class IdentityChatClient(
        IChatClient innerClient,
        ModelIdentity identity) : IChatClient
    {
        private readonly IChatClient _innerClient = innerClient;
        private readonly ModelIdentity _identity = identity;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            _innerClient.GetResponseAsync(messages, options, cancellationToken);

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            _innerClient.GetStreamingResponseAsync(messages, options, cancellationToken);

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(ModelIdentity) && serviceKey is null
                ? _identity
                : _innerClient.GetService(serviceType, serviceKey);

        public void Dispose() => _innerClient.Dispose();
    }
}
