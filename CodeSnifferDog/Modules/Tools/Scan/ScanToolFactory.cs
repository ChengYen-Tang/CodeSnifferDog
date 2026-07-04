using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools;

namespace CodeSnifferDog.Modules.Tools.Scan;

/// <summary>
/// Creates the AI tools exposed to scan agents and verifiers.
/// </summary>
internal static class ScanToolFactory
{
    /// <summary>
    /// Creates the tools used by scan agents.
    /// </summary>
    /// <param name="callbacks">Callbacks invoked by the created tools.</param>
    /// <returns>The scan-agent tools.</returns>
    public static IList<AITool> CreateAgentTools(ScanAgentToolCallbacks callbacks)
        =>
    [
        AIFunctionFactory.Create(
            callbacks.AddScanProjectTool,
            "AddScanProject",
            "Add one discovered project unit to the current scan result.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.AddScanProjectsTool,
            "AddScanProjects",
            "Add multiple discovered project units to the current scan result.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.DeleteScanProjectTool,
            "DeleteScanProject",
            "Delete an existing scan project from the current scan result by its id.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.ListScanProjectsTool,
            "ListScanProjects",
            "List all scan projects currently stored for this scan attempt.",
            serializerOptions: null),
    ];

    /// <summary>
    /// Creates the tools used by scan verifiers.
    /// </summary>
    /// <param name="callbacks">Callbacks invoked by the created tools.</param>
    /// <returns>The scan-verifier tools.</returns>
    public static IList<AITool> CreateVerifierTools(ScanVerifierToolCallbacks callbacks)
        =>
    [
        AIFunctionFactory.Create(
            callbacks.ListScanProjectsTool,
            "ListScanProjects",
            "List all scan projects currently stored for this scan attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            callbacks.SubmitReviewVerdictTool,
            "SubmitReviewVerdict",
            "Submit the verifier approval or rejection for the current scan result.",
            serializerOptions: null),
    ];
}

/// <summary>
/// Groups callbacks used by scan-agent tools.
/// </summary>
/// <param name="AddScanProjectTool">Callback for adding one scan project.</param>
/// <param name="AddScanProjectsTool">Callback for adding multiple scan projects.</param>
/// <param name="DeleteScanProjectTool">Callback for deleting one scan project.</param>
/// <param name="ListScanProjectsTool">Callback for listing stored scan projects.</param>
internal readonly record struct ScanAgentToolCallbacks(
    AddScanProjectToolCallback AddScanProjectTool,
    AddScanProjectsToolCallback AddScanProjectsTool,
    DeleteScanProjectToolCallback DeleteScanProjectTool,
    ListScanProjectsToolCallback ListScanProjectsTool);

/// <summary>
/// Groups callbacks used by scan-verifier tools.
/// </summary>
/// <param name="ListScanProjectsTool">Callback for listing stored scan projects.</param>
/// <param name="SubmitReviewVerdictTool">Callback for submitting the verifier verdict.</param>
internal readonly record struct ScanVerifierToolCallbacks(
    ListScanProjectsToolCallback ListScanProjectsTool,
    SubmitReviewVerdictToolCallback SubmitReviewVerdictTool);

/// <summary>
/// Represents the callback used to add one scan project.
/// </summary>
internal delegate ValueTask<AddScanProjectResult> AddScanProjectToolCallback(
    string ProjectName,
    string ProjectPath,
    string ProjectType,
    string Reason,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to add multiple scan projects.
/// </summary>
internal delegate ValueTask<AddScanProjectsResult> AddScanProjectsToolCallback(
    IReadOnlyList<AddScanProjectArgs> Projects,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to delete one stored scan project.
/// </summary>
internal delegate ValueTask<bool> DeleteScanProjectToolCallback(
    string ScanProjectId,
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to list stored scan projects.
/// </summary>
internal delegate ValueTask<IReadOnlyList<StoredScanProject>> ListScanProjectsToolCallback(
    CancellationToken cancellationToken);

/// <summary>
/// Represents the callback used to submit the verifier verdict.
/// </summary>
internal delegate ValueTask<bool> SubmitReviewVerdictToolCallback(
    bool Approved,
    string Message,
    CancellationToken cancellationToken);
