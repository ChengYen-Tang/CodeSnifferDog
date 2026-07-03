using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.Json.Nodes;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ChatClientProviderTests
{
    [TestMethod]
    public void LoadExtraBodyObject_ReadsNestedOpenAICompatibleExtraBody()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{InferenceProviderOptions.SectionName}:Provider"] = "openai-compatible",
                [$"{InferenceProviderOptions.SectionName}:OpenAICompatible:ExtraBody:reasoning_effort"] = "high",
                [$"{InferenceProviderOptions.SectionName}:OpenAICompatible:ExtraBody:parallel_tool_calls"] = "true",
                [$"{InferenceProviderOptions.SectionName}:OpenAICompatible:ExtraBody:metadata:tier"] = "gold",
            })
            .Build();

        JsonObject? extraBody = OpenAICompatibleInferenceProviderOptions.ParseExtraBody(
            configuration
                .GetSection(InferenceProviderOptions.SectionName)
                .GetSection(nameof(InferenceProviderOptions.OpenAICompatible))
                .GetSection(nameof(OpenAICompatibleInferenceProviderOptions.ExtraBody)));

        Assert.IsNotNull(extraBody);
        Assert.AreEqual("high", extraBody["reasoning_effort"]?.GetValue<string>());
        Assert.IsTrue(extraBody["parallel_tool_calls"]?.GetValue<bool>() ?? false);
        Assert.AreEqual("gold", extraBody["metadata"]?["tier"]?.GetValue<string>());
    }

    [TestMethod]
    public void OpenAICompatibleExtraBody_DoesNotApplyToOpenAIProvider()
    {
        InferenceProviderOptions options = new()
        {
            Provider = "openai",
            OpenAI = new OpenAIInferenceProviderOptions
            {
                ApiKey = "key",
                ModelId = "gpt-4.1"
            },
            OpenAICompatible = new OpenAICompatibleInferenceProviderOptions
            {
                Endpoint = "http://localhost:8000/v1",
                ModelId = "qwen2.5-coder",
                ExtraBody = new JsonObject
                {
                    ["reasoning_effort"] = "high"
                }
            }
        };

        ProjectChatClientProvider chatClientProvider = new(
            Options.Create(options));

        FieldInfo extraBodyField = typeof(ProjectChatClientProvider)
            .GetField("_extraBody", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("_extraBody field was not found.");

        Assert.IsNull(extraBodyField.GetValue(chatClientProvider));
    }

    [TestMethod]
    public void IsReady_RespectsProviderSpecificRequiredFields()
    {
        (string Provider, bool ExpectedIsReady)[] testCases =
        [
            ("openai", true),
            ("azure-openai", true),
            ("vllm", true),
            ("sglang", true),
            ("unknown", false),
        ];

        foreach ((string provider, bool expectedIsReady) in testCases)
        {
            InferenceProviderOptions options = provider switch
            {
                "openai" => new InferenceProviderOptions
                {
                    Provider = provider,
                    OpenAI = new OpenAIInferenceProviderOptions
                    {
                        ApiKey = "key",
                        ModelId = "gpt-4.1"
                    }
                },
                "azure-openai" => new InferenceProviderOptions
                {
                    Provider = provider,
                    AzureOpenAI = new AzureOpenAIInferenceProviderOptions
                    {
                        ApiKey = "key",
                        Endpoint = "https://example.openai.azure.com/",
                        DeploymentName = "gpt-4.1"
                    }
                },
                "unknown" => new InferenceProviderOptions
                {
                    Provider = provider
                },
                _ => new InferenceProviderOptions
                {
                    Provider = provider,
                    OpenAICompatible = new OpenAICompatibleInferenceProviderOptions
                    {
                        Endpoint = "http://localhost:8000/v1",
                        ModelId = "qwen2.5-coder"
                    }
                }
            };

            ProjectChatClientProvider chatClientProvider = new(
                Options.Create(options));

            Assert.AreEqual(expectedIsReady, chatClientProvider.IsReady);
        }
    }

    [TestMethod]
    public void InferenceOptions_BindsRequestTimeoutSeconds()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{InferenceProviderOptions.SectionName}:Provider"] = "vllm",
                [$"{InferenceProviderOptions.SectionName}:RequestTimeoutSeconds"] = "300",
                [$"{InferenceProviderOptions.SectionName}:OpenAICompatible:Endpoint"] = "http://localhost:8000/v1",
                [$"{InferenceProviderOptions.SectionName}:OpenAICompatible:ModelId"] = "qwen2.5-coder",
            })
            .Build();

        InferenceProviderOptions options = new();
        configuration.GetSection(InferenceProviderOptions.SectionName).Bind(options);

        Assert.AreEqual(300, options.RequestTimeoutSeconds);
    }
}
