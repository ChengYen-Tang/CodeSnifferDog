namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework;

public interface IReactiveExceptionDecider
{
    bool ShouldRetryWithReactiveCompaction(ModelInvocationException exception);
}
