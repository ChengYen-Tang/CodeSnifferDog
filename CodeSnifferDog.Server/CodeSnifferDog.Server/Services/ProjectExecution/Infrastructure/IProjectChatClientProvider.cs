using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

public interface IProjectChatClientProvider
{
    bool IsReady { get; }

    IChatClient CreateChatClient();
}
