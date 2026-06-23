using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools;

namespace CodeSnifferDog.Modules.Tools.Scan;

internal static class ScanToolFactory
{
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

internal readonly record struct ScanAgentToolCallbacks(
    AddScanProjectToolCallback AddScanProjectTool,
    AddScanProjectsToolCallback AddScanProjectsTool,
    DeleteScanProjectToolCallback DeleteScanProjectTool,
    ListScanProjectsToolCallback ListScanProjectsTool);

internal readonly record struct ScanVerifierToolCallbacks(
    ListScanProjectsToolCallback ListScanProjectsTool,
    SubmitReviewVerdictToolCallback SubmitReviewVerdictTool);

internal delegate ValueTask<AddScanProjectResult> AddScanProjectToolCallback(
    string ProjectName,
    string ProjectPath,
    string ProjectType,
    string Reason,
    CancellationToken cancellationToken);

internal delegate ValueTask<AddScanProjectsResult> AddScanProjectsToolCallback(
    IReadOnlyList<AddScanProjectArgs> Projects,
    CancellationToken cancellationToken);

internal delegate ValueTask<bool> DeleteScanProjectToolCallback(
    string ScanProjectId,
    CancellationToken cancellationToken);

internal delegate ValueTask<IReadOnlyList<StoredScanProject>> ListScanProjectsToolCallback(
    CancellationToken cancellationToken);

internal delegate ValueTask<bool> SubmitReviewVerdictToolCallback(
    bool Approved,
    string Message,
    CancellationToken cancellationToken);
