using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

public interface IProjectChatClientProvider
{
    bool IsReady { get; }

    IChatClient CreateChatClient();
}
