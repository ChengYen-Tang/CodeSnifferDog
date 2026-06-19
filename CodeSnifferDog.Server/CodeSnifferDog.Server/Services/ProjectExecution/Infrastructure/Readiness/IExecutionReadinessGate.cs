namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Readiness;

internal interface IExecutionReadinessGate
{
    ExecutionReadinessResult Check();
}
