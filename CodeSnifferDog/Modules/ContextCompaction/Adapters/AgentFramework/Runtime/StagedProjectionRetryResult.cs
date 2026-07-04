namespace CodeSnifferDog.Modules.ContextCompaction.Adapters.AgentFramework.Runtime;

/// <summary>
/// Represents the outcome of a staged-projection retry attempt.
/// </summary>
/// <typeparam name="T">Value type returned by the staged retry.</typeparam>
internal sealed class StagedProjectionRetryResult<T>
{
    private StagedProjectionRetryResult(bool succeeded, T? value)
    {
        Succeeded = succeeded;
        Value = value;
    }

    /// <summary>
    /// Gets whether the staged retry ran successfully.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the staged retry value when <see cref="Succeeded" /> is <see langword="true" />.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Creates a result indicating that staged retry was not run or did not succeed.
    /// </summary>
    /// <returns>A non-success result with no value.</returns>
    public static StagedProjectionRetryResult<T> NotRun() => new(false, default);

    /// <summary>
    /// Creates a successful staged retry result.
    /// </summary>
    /// <param name="value">Value returned by the staged retry.</param>
    /// <returns>A success result containing <paramref name="value" />.</returns>
    public static StagedProjectionRetryResult<T> Success(T value) => new(true, value);
}
