namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

/// <summary>
/// Decides whether a model invocation failure should trigger reactive compaction retry.
/// </summary>
public interface IReactiveExceptionDecider
{
    /// <summary>
    /// Determines whether the supplied model invocation failure should be retried with reactive compaction.
    /// </summary>
    /// <param name="exception">Model invocation failure to evaluate.</param>
    /// <returns><see langword="true" /> when reactive compaction retry should be attempted.</returns>
    bool ShouldRetryWithReactiveCompaction(ModelInvocationException exception);
}
