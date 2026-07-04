namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation;

/// <summary>
/// Describes the cleanup and persistence work to perform after execution is canceled.
/// </summary>
/// <param name="ShouldUpdateDatabase">Whether the project status should be persisted.</param>
/// <param name="ShouldDeleteUploadedZip">Whether the uploaded archive should be deleted.</param>
/// <param name="ShouldDeleteExtractedProject">Whether the extracted repository should be deleted.</param>
internal readonly record struct Outcome(
    bool ShouldUpdateDatabase,
    bool ShouldDeleteUploadedZip,
    bool ShouldDeleteExtractedProject)
{
    /// <summary>
    /// Gets the cancellation outcome for explicit user cancellations.
    /// </summary>
    public static Outcome UserCanceled { get; } = new(
        ShouldUpdateDatabase: true,
        ShouldDeleteUploadedZip: true,
        ShouldDeleteExtractedProject: true);

    /// <summary>
    /// Gets the cancellation outcome used during host shutdown so work can be recovered later.
    /// </summary>
    public static Outcome PreserveForRecovery { get; } = new(
        ShouldUpdateDatabase: false,
        ShouldDeleteUploadedZip: false,
        ShouldDeleteExtractedProject: false);
}
