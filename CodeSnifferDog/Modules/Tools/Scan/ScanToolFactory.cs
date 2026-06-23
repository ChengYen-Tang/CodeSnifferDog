using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.Tools.Scan;

internal static class ScanToolFactory
{
    public static IList<AITool> CreateAgentTools(
        Delegate addScanProjectTool,
        Delegate addScanProjectsTool,
        Delegate deleteScanProjectTool,
        Delegate listScanProjectsTool)
        =>
    [
        AIFunctionFactory.Create(
            addScanProjectTool,
            "AddScanProject",
            "Add one discovered project unit to the current scan result.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            addScanProjectsTool,
            "AddScanProjects",
            "Add multiple discovered project units to the current scan result.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            deleteScanProjectTool,
            "DeleteScanProject",
            "Delete an existing scan project from the current scan result by its id.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            listScanProjectsTool,
            "ListScanProjects",
            "List all scan projects currently stored for this scan attempt.",
            serializerOptions: null),
    ];

    public static IList<AITool> CreateVerifierTools(
        Delegate listScanProjectsTool,
        Delegate submitReviewVerdictTool)
        =>
    [
        AIFunctionFactory.Create(
            listScanProjectsTool,
            "ListScanProjects",
            "List all scan projects currently stored for this scan attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            submitReviewVerdictTool,
            "SubmitReviewVerdict",
            "Submit the verifier approval or rejection for the current scan result.",
            serializerOptions: null),
    ];
}
