namespace CodeSnifferDog.Workflows.Common;

/// <summary>
/// Stores common retry-related workflow messages.
/// </summary>
internal static class WorkflowRetryMessages
{
    /// <summary>
    /// Message shown when a verifier finishes without submitting a verdict.
    /// </summary>
    public const string MissingVerifierVerdictMessage =
        "The verifier finished without submitting a verdict. Submit a verdict before finishing.";
}
