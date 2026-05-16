using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json.Nodes;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

public sealed class ProjectChatClientProvider(
    IOptions<InferenceProviderOptions> options) : IProjectChatClientProvider
{
    private readonly InferenceProviderOptions _options = options.Value;
    private readonly JsonObject? _extraBody = IsOpenAICompatibleProvider(options.Value.Provider)
        ? options.Value.OpenAICompatible.ExtraBody
        : null;

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

    public IChatClient CreateChatClient()
    {
        if (!HasRequiredConfiguration())
            throw new InvalidOperationException("Inference provider is not configured.");

        string provider = _options.Provider.Trim();

        if (IsAzureOpenAIProvider(provider))
        {
            AzureOpenAIInferenceProviderOptions azureOptions = _options.AzureOpenAI;
            AzureOpenAIClientOptions azureClientOptions = new();
            ApplyNetworkTimeout(azureClientOptions);
            return ConfigureChatClient(new AzureOpenAIClient(
                    new Uri(azureOptions.Endpoint!.Trim()),
                    new AzureKeyCredential(azureOptions.ApiKey!.Trim()),
                    azureClientOptions)
                .GetChatClient(azureOptions.DeploymentName!.Trim())
                .AsIChatClient());
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

        return ConfigureChatClient(new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
            .GetChatClient(modelId)
            .AsIChatClient());
    }

    private void ApplyNetworkTimeout(OpenAIClientOptions clientOptions)
    {
        if (_options.RequestTimeoutSeconds is > 0)
            clientOptions.NetworkTimeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds.Value);
    }

    private void ApplyNetworkTimeout(AzureOpenAIClientOptions clientOptions)
    {
        if (_options.RequestTimeoutSeconds is > 0)
            clientOptions.NetworkTimeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds.Value);
    }

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

    private static object CreateDefaultRawRequest(IChatClient chatClient)
    {
        string typeName = chatClient.GetType().FullName ?? string.Empty;
        return typeName.Contains("Responses", StringComparison.OrdinalIgnoreCase)
            ? new CreateResponseOptions()
            : new ChatCompletionOptions();
    }

    private object ConfigureRawRequest(object request)
    {
        switch (request)
        {
            case ChatCompletionOptions chatCompletionOptions:
                ApplyExtraBodyPatch(chatCompletionOptions.Patch);
                chatCompletionOptions.AllowParallelToolCalls = true;
                chatCompletionOptions.Patch.Set("$.parallel_tool_calls"u8, true);
                return chatCompletionOptions;
            case CreateResponseOptions responseOptions:
                ApplyExtraBodyPatch(responseOptions.Patch);
                responseOptions.ParallelToolCallsEnabled = true;
                responseOptions.Patch.Set("$.parallel_tool_calls"u8, true);
                return responseOptions;
            default:
                return request;
        }
    }

    private void ApplyExtraBodyPatch(JsonPatch patch)
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

            patch.Set(jsonPath, BinaryData.FromString(value.ToJsonString()));
        }
    }

    private static bool IsSupportedProvider(string provider) =>
        IsOpenAIProvider(provider) || IsAzureOpenAIProvider(provider) || IsOpenAICompatibleProvider(provider);

    private bool HasRequiredConfiguration() =>
        !string.IsNullOrWhiteSpace(_options.Provider) &&
        IsSupportedProvider(_options.Provider) &&
        HasRequiredProviderConfiguration(_options.Provider);

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

    private static bool IsOpenAIProvider(string provider) =>
        string.Equals(provider.Trim(), "openai", StringComparison.OrdinalIgnoreCase);

    private static bool IsAzureOpenAIProvider(string provider) =>
        string.Equals(provider.Trim(), "azure-openai", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider.Trim(), "azure openai", StringComparison.OrdinalIgnoreCase);

    private static bool IsOpenAICompatibleProvider(string provider) =>
        string.Equals(provider.Trim(), "openai-compatible", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider.Trim(), "vllm", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider.Trim(), "sglang", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider.Trim(), "local", StringComparison.OrdinalIgnoreCase);
}
