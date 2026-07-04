namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Readiness;

/// <summary>
/// Checks whether the execution subsystem has the dependencies required to start work.
/// </summary>
internal interface IGate
{
    /// <summary>
    /// Evaluates whether project execution is ready to run.
    /// </summary>
    /// <returns>The readiness result, including an explanatory reason when not ready.</returns>
    Result Check();
}
