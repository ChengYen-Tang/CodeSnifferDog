using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Agents.Common.TokenUsage;

/// <summary>
/// Reuses a fingerprint for an unchanged, normalized chat-request contract.
/// </summary>
/// <remarks>
/// The cache is deliberately conservative. It only caches the framework's known immutable tool
/// declarations and options whose mutable collections can be snapshotted cheaply. Unsupported or
/// custom representations are delegated to <see cref="ChatRequestFingerprint.Create" /> without caching.
/// </remarks>
internal sealed class ChatRequestFingerprintCache
{
    private readonly object _syncRoot = new();
    private CachedContract? _cachedContract;
    private string? _cachedFingerprint;

    /// <summary>
    /// Gets the fingerprint for the supplied options, reusing the last unchanged contract when safe.
    /// </summary>
    /// <param name="options">Options that describe the provider request.</param>
    /// <returns>The fingerprint, or <see langword="null" /> when the request cannot be identified safely.</returns>
    public string? Get(ChatOptions? options)
    {
        if (options is null)
            return ChatRequestFingerprint.Create(options);

        lock (_syncRoot)
        {
            if (_cachedContract is not null && _cachedContract.Matches(options))
                return _cachedFingerprint;
        }

        if (!CachedContract.TryCreate(options, out CachedContract? contract) || contract is null)
            return ChatRequestFingerprint.Create(options);

        // Do not hold the cache lock while reflecting or serializing a large tool schema. A concurrent
        // miss may compute the same value once; the final publication is still serialized below.
        string? fingerprint = ChatRequestFingerprint.Create(options);

        lock (_syncRoot)
        {
            if (_cachedContract is not null && _cachedContract.Matches(options))
                return _cachedFingerprint;

            _cachedContract = contract;
            _cachedFingerprint = fingerprint;
            return fingerprint;
        }
    }

    private sealed class CachedContract
    {
        private readonly string? _conversationId;
        private readonly string? _instructions;
        private readonly float? _temperature;
        private readonly int? _maxOutputTokens;
        private readonly float? _topP;
        private readonly int? _topK;
        private readonly float? _frequencyPenalty;
        private readonly float? _presencePenalty;
        private readonly long? _seed;
        private readonly string? _modelId;
        private readonly bool? _allowMultipleToolCalls;
        private readonly bool? _allowBackgroundResponses;
        private readonly ReasoningEffort? _reasoningEffort;
        private readonly ReasoningOutput? _reasoningOutput;
        private readonly ChatResponseFormat? _responseFormat;
        private readonly ChatToolMode? _toolMode;
        private readonly string[]? _stopSequences;
        private readonly AITool[]? _tools;

        private CachedContract(ChatOptions options, string[]? stopSequences, AITool[]? tools)
        {
            _conversationId = options.ConversationId;
            _instructions = options.Instructions;
            _temperature = options.Temperature;
            _maxOutputTokens = options.MaxOutputTokens;
            _topP = options.TopP;
            _topK = options.TopK;
            _frequencyPenalty = options.FrequencyPenalty;
            _presencePenalty = options.PresencePenalty;
            _seed = options.Seed;
            _modelId = options.ModelId;
            _allowMultipleToolCalls = options.AllowMultipleToolCalls;
            _allowBackgroundResponses = options.AllowBackgroundResponses;
            _reasoningEffort = options.Reasoning?.Effort;
            _reasoningOutput = options.Reasoning?.Output;
            _responseFormat = options.ResponseFormat;
            _toolMode = options.ToolMode;
            _stopSequences = stopSequences;
            _tools = tools;
        }

        public static bool TryCreate(ChatOptions options, out CachedContract? contract)
        {
            contract = null;

            if (options.GetType() != typeof(ChatOptions) ||
                options.RawRepresentationFactory is not null ||
                options.AdditionalProperties is not null ||
                options.ContinuationToken is not null ||
                !IsSupportedReasoning(options.Reasoning) ||
                !IsSupportedResponseFormat(options.ResponseFormat) ||
                !IsSupportedToolMode(options.ToolMode))
            {
                return false;
            }

            string[]? stopSequences = options.StopSequences is { } sequences
                ? [.. sequences]
                : null;

            AITool[]? tools = null;
            if (options.Tools is { } configuredTools)
            {
                tools = new AITool[configuredTools.Count];
                for (int index = 0; index < configuredTools.Count; index++)
                {
                    AITool? tool = configuredTools[index];
                    if (tool is null || !IsCacheableTool(tool))
                        return false;

                    tools[index] = tool;
                }
            }

            contract = new CachedContract(options, stopSequences, tools);
            return true;
        }

        public bool Matches(ChatOptions options) =>
            options.GetType() == typeof(ChatOptions) &&
            options.RawRepresentationFactory is null &&
            options.AdditionalProperties is null &&
            options.ContinuationToken is null &&
            IsSupportedReasoning(options.Reasoning) &&
            IsSupportedResponseFormat(options.ResponseFormat) &&
            IsSupportedToolMode(options.ToolMode) &&
            string.Equals(_conversationId, options.ConversationId, StringComparison.Ordinal) &&
            string.Equals(_instructions, options.Instructions, StringComparison.Ordinal) &&
            _temperature == options.Temperature &&
            _maxOutputTokens == options.MaxOutputTokens &&
            _topP == options.TopP &&
            _topK == options.TopK &&
            _frequencyPenalty == options.FrequencyPenalty &&
            _presencePenalty == options.PresencePenalty &&
            _seed == options.Seed &&
            string.Equals(_modelId, options.ModelId, StringComparison.Ordinal) &&
            _allowMultipleToolCalls == options.AllowMultipleToolCalls &&
            _allowBackgroundResponses == options.AllowBackgroundResponses &&
            _reasoningEffort == options.Reasoning?.Effort &&
            _reasoningOutput == options.Reasoning?.Output &&
            ReferenceEquals(_responseFormat, options.ResponseFormat) &&
            ReferenceEquals(_toolMode, options.ToolMode) &&
            SequenceEqual(_stopSequences, options.StopSequences) &&
            SequenceEqual(_tools, options.Tools);

        private static bool IsSupportedReasoning(ReasoningOptions? reasoning) =>
            reasoning is null || reasoning.GetType() == typeof(ReasoningOptions);

        private static bool IsSupportedResponseFormat(ChatResponseFormat? responseFormat) =>
            responseFormat is null ||
            responseFormat is ChatResponseFormatText ||
            responseFormat is ChatResponseFormatJson;

        private static bool IsSupportedToolMode(ChatToolMode? toolMode) =>
            toolMode is null ||
            toolMode is AutoChatToolMode ||
            toolMode is NoneChatToolMode ||
            toolMode is RequiredChatToolMode;

        private static bool IsCacheableTool(AITool tool) =>
            tool is AIFunctionDeclaration function &&
            function.GetType().IsSealed &&
            function.GetType().Assembly == typeof(AIFunctionDeclaration).Assembly &&
            function.AdditionalProperties is null;

        private static bool SequenceEqual(string[]? left, IList<string>? right)
        {
            if (left is null || right is null)
                return left is null && right is null;
            if (left.Length != right.Count)
                return false;

            for (int index = 0; index < left.Length; index++)
            {
                if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static bool SequenceEqual(AITool[]? left, IList<AITool>? right)
        {
            if (left is null || right is null)
                return left is null && right is null;
            if (left.Length != right.Count)
                return false;

            for (int index = 0; index < left.Length; index++)
            {
                if (!ReferenceEquals(left[index], right[index]) || !IsCacheableTool(right[index]))
                    return false;
            }

            return true;
        }
    }
}
