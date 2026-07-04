namespace CodeSnifferDog.Models.ReviewAgentTeam.Analysis;

/// <summary>
/// Describes whether analysis completed successfully and whether reports should be persisted.
/// </summary>
public sealed class CompletionDecision
{
    /// <summary>
    /// Gets whether the overall analysis result should be treated as successful.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Gets whether generated reports should be persisted.
    /// </summary>
    public required bool ShouldPersistReports { get; init; }

    /// <summary>
    /// Gets the failure message when analysis did not complete successfully.
    /// </summary>
    public string? FailureMessage { get; init; }
}
