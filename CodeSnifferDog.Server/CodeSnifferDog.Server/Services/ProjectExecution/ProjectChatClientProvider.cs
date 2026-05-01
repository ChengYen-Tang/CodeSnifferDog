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
    IOptions<InferenceProviderOptions> options,
    IHostEnvironment hostEnvironment) : IProjectChatClientProvider
{
    private readonly InferenceProviderOptions _options = options.Value;
    private readonly JsonObject? _extraBody = LoadExtraBodyObject(hostEnvironment.ContentRootPath, hostEnvironment.EnvironmentName);

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
        string modelId = _options.ModelId!.Trim();
        string apiKey = ResolveApiKey(provider);

        if (IsAzureOpenAIProvider(provider))
            return ConfigureChatClient(new AzureOpenAIClient(new Uri(_options.Endpoint!.Trim()), new AzureKeyCredential(apiKey))
                .GetChatClient(modelId)
                .AsIChatClient());

        OpenAIClientOptions clientOptions = new();
        if (!string.IsNullOrWhiteSpace(_options.Endpoint))
            clientOptions.Endpoint = new Uri(_options.Endpoint.Trim());

        return ConfigureChatClient(new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
            .GetChatClient(modelId)
            .AsIChatClient());
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

    internal static JsonObject? LoadExtraBodyObject(string contentRootPath, string environmentName)
    {
        JsonObject merged = [];

        MergeExtraBodyFile(Path.Combine(contentRootPath, "appsettings.json"), merged);

        if (!string.IsNullOrWhiteSpace(environmentName))
            MergeExtraBodyFile(Path.Combine(contentRootPath, $"appsettings.{environmentName}.json"), merged);

        return merged.Count == 0 ? null : merged;
    }

    private static void MergeExtraBodyFile(string path, JsonObject target)
    {
        if (!File.Exists(path))
            return;

        JsonNode? root = JsonNode.Parse(File.ReadAllText(path));
        if (root is not JsonObject rootObject)
            return;

        JsonNode? extraBodyNode = rootObject[InferenceProviderOptions.SectionName]?["extra_body"];
        if (extraBodyNode is null)
            return;

        if (extraBodyNode is not JsonObject extraBodyObject)
            throw new InvalidOperationException($"'{InferenceProviderOptions.SectionName}:extra_body' must be a JSON object.");

        MergeJsonObjects(target, (JsonObject)extraBodyObject.DeepClone());
    }

    private static void MergeJsonObjects(JsonObject target, JsonObject source)
    {
        foreach ((string key, JsonNode? sourceValue) in source)
        {
            if (target[key] is JsonObject targetObject && sourceValue is JsonObject sourceObject)
            {
                MergeJsonObjects(targetObject, sourceObject);
                continue;
            }

            target[key] = sourceValue;
        }
    }

    private string ResolveApiKey(string provider)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            return _options.ApiKey.Trim();

        if (IsOpenAICompatibleProvider(provider))
            return "unused";

        throw new InvalidOperationException("Inference provider API key is not configured.");
    }

    private static bool IsSupportedProvider(string provider) =>
        IsOpenAIProvider(provider) || IsAzureOpenAIProvider(provider) || IsOpenAICompatibleProvider(provider);

    private bool HasRequiredConfiguration() =>
        !string.IsNullOrWhiteSpace(_options.Provider) &&
        !string.IsNullOrWhiteSpace(_options.ModelId) &&
        IsSupportedProvider(_options.Provider) &&
        (!RequiresApiKey(_options.Provider) || !string.IsNullOrWhiteSpace(_options.ApiKey)) &&
        (IsOpenAIProvider(_options.Provider) || !string.IsNullOrWhiteSpace(_options.Endpoint));

    private static bool RequiresApiKey(string provider) =>
        IsOpenAIProvider(provider) || IsAzureOpenAIProvider(provider);

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
