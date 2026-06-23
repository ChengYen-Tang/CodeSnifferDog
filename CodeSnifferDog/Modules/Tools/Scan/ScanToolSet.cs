using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace CodeSnifferDog.Modules.Tools.Scan;

public sealed class ScanToolSet
{
    private readonly ScanProjectToolService _projectToolService;
    private readonly ReviewVerdictToolService _verdictToolService;

    public ScanToolSet(IScanProjectStore scanProjectStore, ReviewVerdictBuffer verdictBuffer)
        : this(new ScanProjectToolService(scanProjectStore), new ReviewVerdictToolService(verdictBuffer))
    {
    }

    internal ScanToolSet(
        ScanProjectToolService projectToolService,
        ReviewVerdictToolService verdictToolService)
    {
        _projectToolService = projectToolService;
        _verdictToolService = verdictToolService;
    }

    public IList<AITool> CreateScanAgentTools()
        =>
        ScanToolFactory.CreateAgentTools(new ScanAgentToolCallbacks(
            AddScanProjectToolAsync,
            AddScanProjectsToolAsync,
            DeleteScanProjectToolAsync,
            ListScanProjectsAsync));

    public IList<AITool> CreateVerifierTools()
        =>
        ScanToolFactory.CreateVerifierTools(new ScanVerifierToolCallbacks(
            ListScanProjectsAsync,
            SubmitReviewVerdictToolAsync));

    [Description("Add one discovered project unit to the current scan result.")]
    private ValueTask<AddScanProjectResult> AddScanProjectToolAsync(
        [Description("The display name of the discovered project unit.")]
        string ProjectName,
        [Description("The repository-relative path or canonical path that identifies the discovered project unit.")]
        string ProjectPath,
        [Description("The project category or file type, such as .csproj, package.json, or directory-based module.")]
        string ProjectType,
        [Description("Why this project unit should enter the next planning stage.")]
        string Reason,
        CancellationToken cancellationToken) =>
        AddScanProjectAsync(
            new AddScanProjectArgs
            {
                ProjectName = ProjectName,
                ProjectPath = ProjectPath,
                ProjectType = ProjectType,
                Reason = Reason,
            },
            cancellationToken);

    [Description("Add multiple discovered project units to the current scan result.")]
    private ValueTask<AddScanProjectsResult> AddScanProjectsToolAsync(
        [Description("The project units to add to the current scan result.")]
        IReadOnlyList<AddScanProjectArgs> Projects,
        CancellationToken cancellationToken) =>
        AddScanProjectsAsync(
            new AddScanProjectsArgs
            {
                Projects = Projects,
            },
            cancellationToken);

    [Description("Delete one existing scan project from the current scan result by its id.")]
    private ValueTask<bool> DeleteScanProjectToolAsync(
        [Description("The id of the stored scan project to delete from the current scan result.")]
        string ScanProjectId,
        CancellationToken cancellationToken) =>
        DeleteScanProjectAsync(
            new DeleteScanProjectArgs
            {
                ScanProjectId = ScanProjectId,
            },
            cancellationToken);

    [Description("Submit the verifier approval or rejection for the current scan result.")]
    private ValueTask<bool> SubmitReviewVerdictToolAsync(
        [Description("True when the current scan result is approved. False when more work is required.")]
        bool Approved,
        [Description("The approval note or the rejection reason that explains what the scan agent should keep or fix.")]
        string Message,
        CancellationToken cancellationToken) =>
        SubmitReviewVerdictAsync(
            new SubmitReviewVerdictArgs
            {
                Approved = Approved,
                Message = Message,
            },
            cancellationToken);

    public ValueTask<AddScanProjectResult> AddScanProjectAsync(
        AddScanProjectArgs args,
        CancellationToken cancellationToken) =>
        _projectToolService.AddScanProjectAsync(args, cancellationToken);

    public ValueTask<AddScanProjectsResult> AddScanProjectsAsync(
        AddScanProjectsArgs args,
        CancellationToken cancellationToken) =>
        _projectToolService.AddScanProjectsAsync(args, cancellationToken);

    public ValueTask<bool> DeleteScanProjectAsync(DeleteScanProjectArgs args, CancellationToken cancellationToken) =>
        _projectToolService.DeleteScanProjectAsync(args, cancellationToken);

    public ValueTask<IReadOnlyList<StoredScanProject>> ListScanProjectsAsync(CancellationToken cancellationToken)
        =>
        _projectToolService.ListScanProjectsAsync(cancellationToken);

    public ValueTask<bool> SubmitReviewVerdictAsync(
        SubmitReviewVerdictArgs args,
        CancellationToken _) =>
        _verdictToolService.SubmitReviewVerdictAsync(args);
}
