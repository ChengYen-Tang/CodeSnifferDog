namespace CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;

internal interface IRuntimeComponentsFactory
{
    RuntimeComponents Create(Guid projectId);
}
