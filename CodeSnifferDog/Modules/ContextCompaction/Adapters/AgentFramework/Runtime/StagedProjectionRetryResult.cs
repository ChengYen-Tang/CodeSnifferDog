namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

internal sealed class StagedProjectionRetryResult<T>
{
    private StagedProjectionRetryResult(bool succeeded, T? value)
    {
        Succeeded = succeeded;
        Value = value;
    }

    public bool Succeeded { get; }

    public T? Value { get; }

    public static StagedProjectionRetryResult<T> NotRun() => new(false, default);

    public static StagedProjectionRetryResult<T> Success(T value) => new(true, value);
}
