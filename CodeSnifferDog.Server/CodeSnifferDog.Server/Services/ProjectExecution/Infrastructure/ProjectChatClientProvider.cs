using Azure;
using Azure.AI.OpenAI;
using CodeSnifferDog.Agents.Common.TokenUsage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CodeSnifferDog.Json;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

/// <summary>
/// Creates chat clients that honor the configured inference provider and request customization settings.
/// </summary>
public sealed class ProjectChatClientProvider(
    IOptions<InferenceProviderOptions> options) : IProjectChatClientProvider
{
    private readonly InferenceProviderOptions _options = options.Value;
    private readonly string? _reasoningEffort = NormalizeReasoningEffort(options.Value.ReasoningEffort);
    private readonly JsonObject? _extraBody = IsOpenAICompatibleProvider(options.Value.Provider)
        ? options.Value.OpenAICompatible.ExtraBody
        : null;

    /// <inheritdoc />
    public bool IsReady
    {
        get
        {
            if (!HasRequiredConfiguration())
                return false;

            try
            {
                using IChatClient chatClient = CreateChatClient();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public IChatClient CreateChatClient()
    {
        if (!HasRequiredConfiguration())
            throw new InvalidOperationException("Inference provider is not configured.");

        string provider = _options.Provider.Trim();

        if (IsAzureOpenAIProvider(provider))
        {
            AzureOpenAIInferenceProviderOptions azureOptions = _options.AzureOpenAI;
            string deploymentId = azureOptions.DeploymentName!.Trim();
            AzureOpenAIClientOptions azureClientOptions = new();
            ApplyNetworkTimeout(azureClientOptions);
            return ChatClientIdentity.Attach(ConfigureChatClient(new AzureOpenAIClient(
                    new Uri(azureOptions.Endpoint!.Trim()),
                    new AzureKeyCredential(azureOptions.ApiKey!.Trim()),
                    azureClientOptions)
                .GetChatClient(deploymentId)
                .AsIChatClient()), deploymentId);
        }

        OpenAIClientOptions clientOptions = new();
        ApplyNetworkTimeout(clientOptions);
        string modelId;
        string apiKey;

        if (IsOpenAIProvider(provider))
        {
            OpenAIInferenceProviderOptions openAIOptions = _options.OpenAI;
            modelId = openAIOptions.ModelId!.Trim();
            apiKey = openAIOptions.ApiKey!.Trim();
        }
        else
        {
            OpenAICompatibleInferenceProviderOptions compatibleOptions = _options.OpenAICompatible;
            modelId = compatibleOptions.ModelId!.Trim();
            apiKey = string.IsNullOrWhiteSpace(compatibleOptions.ApiKey) ? "unused" : compatibleOptions.ApiKey.Trim();
            clientOptions.Endpoint = new Uri(compatibleOptions.Endpoint!.Trim());
        }

        return ChatClientIdentity.Attach(ConfigureChatClient(new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
            .GetChatClient(modelId)
            .AsIChatClient()), modelId);
    }

    /// <summary>
    /// Applies the configured request timeout to OpenAI-compatible client options.
    /// </summary>
    /// <param name="clientOptions">Client options to update.</param>
    private void ApplyNetworkTimeout(OpenAIClientOptions clientOptions)
    {
        if (_options.RequestTimeoutSeconds is > 0)
            clientOptions.NetworkTimeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds.Value);
    }

    /// <summary>
    /// Applies the configured request timeout to Azure OpenAI client options.
    /// </summary>
    /// <param name="clientOptions">Client options to update.</param>
    private void ApplyNetworkTimeout(AzureOpenAIClientOptions clientOptions)
    {
        if (_options.RequestTimeoutSeconds is > 0)
            clientOptions.NetworkTimeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds.Value);
    }

    /// <summary>
    /// Configures the chat client so tool calling and provider-specific raw request mutations are enabled.
    /// </summary>
    /// <param name="chatClient">Chat client returned by the provider SDK.</param>
    /// <returns>The wrapped chat client used by project execution workflows.</returns>
    private IChatClient ConfigureChatClient(IChatClient chatClient) =>
        chatClient
            .AsBuilder()
            .ConfigureOptions(options =>
            {
                options.AllowMultipleToolCalls = true;

                Func<IChatClient, object?>? previousFactory = options.RawRepresentationFactory;
                options.RawRepresentationFactory = client =>
                {
                    object request = previousFactory?.Invoke(client) ?? CreateDefaultRawRequest(client);
                    return ConfigureRawRequest(request);
                };
            })
            .Build();

    /// <summary>
    /// Creates a default raw request object when the provider SDK has not created one yet.
    /// </summary>
    /// <param name="chatClient">Chat client that determines which request type to create.</param>
    /// <returns>A raw request object understood by the current SDK implementation.</returns>
    private static object CreateDefaultRawRequest(IChatClient chatClient)
    {
        string typeName = chatClient.GetType().FullName ?? string.Empty;
        return typeName.Contains("Responses", StringComparison.OrdinalIgnoreCase)
            ? new CreateResponseOptions()
            : new ChatCompletionOptions();
    }

    /// <summary>
    /// Enables parallel tool calls and applies configured request body fields.
    /// </summary>
    /// <param name="request">Raw request object produced by the underlying SDK.</param>
    /// <returns>The updated request object.</returns>
    private object ConfigureRawRequest(object request)
    {
        switch (request)
        {
            case ChatCompletionOptions chatCompletionOptions:
                ref JsonPatch chatCompletionPatch = ref chatCompletionOptions.Patch;
                ApplyExtraBodyPatch(ref chatCompletionPatch);
                ApplyReasoningEffortPatch(ref chatCompletionPatch, "$.reasoning_effort"u8);
                chatCompletionOptions.AllowParallelToolCalls = true;
                chatCompletionPatch.Set("$.parallel_tool_calls"u8, true);
                return chatCompletionOptions;
            case CreateResponseOptions responseOptions:
                ref JsonPatch responsePatch = ref responseOptions.Patch;
                ApplyExtraBodyPatch(ref responsePatch);
                if (_reasoningEffort is not null)
                {
                    responseOptions.ReasoningOptions ??= new ResponseReasoningOptions();
                    ApplyReasoningEffortPatch(ref responsePatch, "$.reasoning.effort"u8);
                }
                responseOptions.ParallelToolCallsEnabled = true;
                responsePatch.Set("$.parallel_tool_calls"u8, true);
                return responseOptions;
            default:
                return request;
        }
    }

    /// <summary>
    /// Applies the configured reasoning effort to the outgoing provider request.
    /// </summary>
    /// <param name="patch">Patch object used to mutate the raw provider request.</param>
    private void ApplyReasoningEffortPatch(ref JsonPatch patch, ReadOnlySpan<byte> jsonPath)
    {
        if (_reasoningEffort is null)
            return;

        patch.Set(
            jsonPath,
            BinaryData.FromString(JsonSerializer.Serialize(_reasoningEffort)));
    }

    /// <summary>
    /// Normalizes an optional reasoning effort value while preserving provider-specific values.
    /// </summary>
    /// <param name="value">Configured reasoning effort.</param>
    /// <returns>A trimmed value, or <see langword="null"/> when no value was configured.</returns>
    private static string? NormalizeReasoningEffort(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Applies the configured extra request body fields to the outgoing provider request.
    /// </summary>
    /// <param name="patch">Patch object used to mutate the raw provider request.</param>
    private void ApplyExtraBodyPatch(ref JsonPatch patch)
    {
        if (_extraBody is null || _extraBody.Count == 0)
            return;

        foreach ((string key, JsonNode? value) in _extraBody)
        {
            byte[] jsonPath = Encoding.UTF8.GetBytes($"$.{key}");

            if (value is null)
            {
                patch.SetNull(jsonPath);
                continue;
            }

            patch.Set(jsonPath, BinaryData.FromString(CodeSnifferDogJson.ToJsonString(value)));
        }
    }

    /// <summary>
    /// Determines whether the provider name refers to a supported inference backend.
    /// </summary>
    /// <param name="provider">Provider name from configuration.</param>
    /// <returns><see langword="true"/> when the provider is supported; otherwise, <see langword="false"/>.</returns>
    private static bool IsSupportedProvider(string provider) =>
        IsOpenAIProvider(provider) || IsAzureOpenAIProvider(provider) || IsOpenAICompatibleProvider(provider);

    /// <summary>
    /// Determines whether the current configuration is sufficient to build a chat client.
    /// </summary>
    /// <returns><see langword="true"/> when all required settings are present; otherwise, <see langword="false"/>.</returns>
    private bool HasRequiredConfiguration() =>
        !string.IsNullOrWhiteSpace(_options.Provider) &&
        IsSupportedProvider(_options.Provider) &&
        HasRequiredProviderConfiguration(_options.Provider);

    /// <summary>
    /// Validates that the required settings exist for the specified provider.
    /// </summary>
    /// <param name="provider">Provider name from configuration.</param>
    /// <returns><see langword="true"/> when the provider-specific settings are complete; otherwise, <see langword="false"/>.</returns>
    private bool HasRequiredProviderConfiguration(string provider)
    {
        if (IsOpenAIProvider(provider))
            return !string.IsNullOrWhiteSpace(_options.OpenAI.ApiKey)
                && !string.IsNullOrWhiteSpace(_options.OpenAI.ModelId);

        if (IsAzureOpenAIProvider(provider))
            return !string.IsNullOrWhiteSpace(_options.AzureOpenAI.Endpoint)
                && !string.IsNullOrWhiteSpace(_options.AzureOpenAI.ApiKey)
                && !string.IsNullOrWhiteSpace(_options.AzureOpenAI.DeploymentName);

        return !string.IsNullOrWhiteSpace(_options.OpenAICompatible.Endpoint)
            && !string.IsNullOrWhiteSpace(_options.OpenAICompatible.ModelId);
    }

    /// <summary>
    /// Determines whether the provider name refers to OpenAI.
    /// </summary>
    /// <param name="provider">Provider name from configuration.</param>
    /// <returns><see langword="true"/> when the provider refers to OpenAI; otherwise, <see langword="false"/>.</returns>
    private static bool IsOpenAIProvider(string provider) =>
        string.Equals(provider.Trim(), "openai", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider.Trim(), nameof(InferenceProviderOptions.OpenAI), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the provider name refers to Azure OpenAI.
    /// </summary>
    /// <param name="provider">Provider name from configuration.</param>
    /// <returns><see langword="true"/> when the provider refers to Azure OpenAI; otherwise, <see langword="false"/>.</returns>
    private static bool IsAzureOpenAIProvider(string provider) =>
        string.Equals(provider.Trim(), nameof(InferenceProviderOptions.AzureOpenAI), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider.Trim(), "azure-openai", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider.Trim(), "azure openai", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the provider name refers to an OpenAI-compatible backend.
    /// </summary>
    /// <param name="provider">Provider name from configuration.</param>
    /// <returns><see langword="true"/> when the provider refers to an OpenAI-compatible backend; otherwise, <see langword="false"/>.</returns>
    private static bool IsOpenAICompatibleProvider(string provider) =>
        string.Equals(provider.Trim(), nameof(InferenceProviderOptions.OpenAICompatible), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider.Trim(), "openai-compatible", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider.Trim(), "vllm", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider.Trim(), "sglang", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider.Trim(), "local", StringComparison.OrdinalIgnoreCase);
}
