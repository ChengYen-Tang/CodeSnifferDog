# Configuration

The server reads `appsettings.json`, then applies environment-specific files such as `appsettings.Development.json`. Environment variables, user secrets, and other standard .NET configuration providers can override these values.

Do not commit API keys or production database credentials.

## Database

The default development configuration uses SQL Server LocalDB. For another SQL Server instance, set the connection string used by the server before starting it. The application runs pending EF Core migrations during startup.

## Inference providers

The provider is selected under `Inference:Provider`. The common `ReasoningEffort` value is optional; leave it `null` when the selected backend does not support it.

### OpenAI

```json
{
  "Inference": {
    "Provider": "OpenAI",
    "ReasoningEffort": "max",
    "OpenAI": {
      "ApiKey": "",
      "ModelId": "gpt-5.6-luna"
    }
  }
}
```

### Azure OpenAI

```json
{
  "Inference": {
    "Provider": "AzureOpenAI",
    "ReasoningEffort": "high",
    "AzureOpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ApiKey": "",
      "DeploymentName": "your-deployment"
    }
  }
}
```

### OpenAI-compatible APIs

```json
{
  "Inference": {
    "Provider": "OpenAICompatible",
    "ReasoningEffort": "max",
    "OpenAICompatible": {
      "Endpoint": "http://localhost:8317/v1",
      "ApiKey": "",
      "ModelId": "gpt-5.6-luna",
      "ExtraBody": {}
    }
  }
}
```

`OpenAICompatible` is the shared transport for CPA, vLLM, SGLang, Ollama, and similar servers. Use `ExtraBody` only for request fields documented by the selected backend. The effective reasoning values are provider/model dependent; `low`, `medium`, and `high` are the most portable choices.

Equivalent environment-variable overrides use double underscores for nesting:

```powershell
$env:Inference__Provider = "OpenAICompatible"
$env:Inference__ReasoningEffort = "high"
$env:Inference__OpenAICompatible__Endpoint = "http://localhost:8317/v1"
$env:Inference__OpenAICompatible__ModelId = "your-model"
```

## Review rules

Review rules are loaded from the application's `rules/` directory. In the source tree, the supplied templates are in [`CodeSnifferDog/rules/`](../CodeSnifferDog/rules/); in a published deployment, use the `rules/` directory beside the application executable.

The loader reads every non-empty Markdown file (`*.md`) directly inside that directory, in filename order. Subdirectories are not included. The file name without `.md` becomes both the rule key and display name, while the complete Markdown document is provided to the review agents as the rule definition.

You can replace the supplied templates or add new top-level Markdown files without changing application code. Keep each file non-empty and use a stable file name if reports or downstream tooling need a stable rule identifier.

The repository currently includes:

- [Maintainability](../CodeSnifferDog/rules/maintainability.md)
- [Performance](../CodeSnifferDog/rules/performance.md)
- [Reliability](../CodeSnifferDog/rules/reliability.md)
- [Security](../CodeSnifferDog/rules/security.md)

## Project execution

The main worker limits are under `ProjectExecution`:

```json
{
  "ProjectExecution": {
    "MaxConcurrentWorkers": 1,
    "MaxQueuedProjects": 100,
    "QueuePollingIntervalSeconds": 5,
    "ExecutionOptions": {
      "MaxParallelAgents": 4,
      "ModelContextWindowTokens": 128000,
      "ContextCompactionMode": "Standard",
      "AgentRunTimeoutSeconds": 3600,
      "MaxConsecutiveAgentRunFailures": 10,
      "MaxMissingSubmissionAttempts": 20,
      "MaxVerifierRejectionAttempts": 20
    }
  }
}
```

- `MaxConcurrentWorkers` limits projects running at the same time.
- `MaxQueuedProjects` bounds the queue.
- `MaxParallelAgents` controls bounded concurrency within one project.
- `ModelContextWindowTokens` is the model context budget used by the review worker.
- `ContextCompactionMode` selects the configured compaction strategy.
- Timeout and retry limits prevent a failed model call or verifier loop from running indefinitely.

Tune concurrency to the available CPU, memory, provider rate limits, and database capacity. Higher parallelism is not automatically faster.

## Runtime paths

- Logs: `<application-base>/logs/`
- Uploaded archives and extracted repositories: `<application-base>/TemporaryStorage/`
- Prompts: `CodeSnifferDog/prompts/` in the source tree and application output
- Rule templates: `CodeSnifferDog/rules/` in the source tree and application output
