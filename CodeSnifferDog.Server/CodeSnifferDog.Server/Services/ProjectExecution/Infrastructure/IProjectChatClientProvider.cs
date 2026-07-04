using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

/// <summary>
/// Creates chat clients for project execution workflows from the configured inference provider settings.
/// </summary>
public interface IProjectChatClientProvider
{
    /// <summary>
    /// Gets a value indicating whether the configured provider can currently create a chat client.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Creates a chat client for project execution workflows.
    /// </summary>
    /// <returns>A configured chat client.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the inference provider is not configured.</exception>
    IChatClient CreateChatClient();
}
