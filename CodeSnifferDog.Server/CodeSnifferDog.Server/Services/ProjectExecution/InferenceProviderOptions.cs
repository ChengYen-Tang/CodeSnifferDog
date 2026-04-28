namespace CodeSnifferDog.Server.Services.ProjectExecution;

public sealed class InferenceProviderOptions
{
    public const string SectionName = "Inference";

    public string Provider { get; init; } = "openai";

    public string? ModelId { get; init; }

    public string? ApiKey { get; init; }

    public string? Endpoint { get; init; }
}
