namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

internal interface IRuntimeFactory
{
    RuntimeContext Create(Guid projectId);
}
