using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Models.Scan.Tools;
using CodeSnifferDog.Modules.Tools.Review;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace CodeSnifferDog.Modules.Tools.Scan;

public sealed class ScanToolSet(IScanProjectStore scanProjectStore, ReviewVerdictBuffer verdictBuffer)
{
    private readonly IScanProjectStore _scanProjectStore = scanProjectStore;
    private readonly ReviewVerdictBuffer _verdictBuffer = verdictBuffer;

    public IList<AITool> CreateScanAgentTools()
        =>
    [
        AIFunctionFactory.Create(
            AddScanProjectToolAsync,
            "AddScanProject",
            "Add one discovered project unit to the current scan result.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            AddScanProjectsToolAsync,
            "AddScanProjects",
            "Add multiple discovered project units to the current scan result.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            DeleteScanProjectToolAsync,
            "DeleteScanProject",
            "Delete an existing scan project from the current scan result by its id.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            ListScanProjectsAsync,
            "ListScanProjects",
            "List all scan projects currently stored for this scan attempt.",
            serializerOptions: null),
    ];

    public IList<AITool> CreateVerifierTools()
        =>
    [
        AIFunctionFactory.Create(
            ListScanProjectsAsync,
            "ListScanProjects",
            "List all scan projects currently stored for this scan attempt.",
            serializerOptions: null),
        AIFunctionFactory.Create(
            SubmitReviewVerdictToolAsync,
            "SubmitReviewVerdict",
            "Submit the verifier approval or rejection for the current scan result.",
            serializerOptions: null),
    ];

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

    public async ValueTask<AddScanProjectResult> AddScanProjectAsync(
        AddScanProjectArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ValidateAddScanProjectArgs(args);

        StoredScanProject storedProject = await _scanProjectStore.AddAsync(
            new ScanProject
            {
                ProjectName = args.ProjectName.Trim(),
                ProjectPath = args.ProjectPath.Trim(),
                ProjectType = args.ProjectType.Trim(),
                Reason = args.Reason.Trim(),
            },
            cancellationToken).ConfigureAwait(false);

        return new AddScanProjectResult
        {
            ScanProjectId = storedProject.ScanProjectId,
        };
    }

    public async ValueTask<AddScanProjectsResult> AddScanProjectsAsync(
        AddScanProjectsArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Projects.Count == 0)
            throw new ArgumentException("At least one scan project is required.", nameof(args));

        foreach (AddScanProjectArgs project in args.Projects)
            ValidateAddScanProjectArgs(project);

        IReadOnlyList<StoredScanProject> storedProjects = await _scanProjectStore.AddRangeAsync(
            [.. args.Projects.Select(project => new ScanProject
            {
                ProjectName = project.ProjectName.Trim(),
                ProjectPath = project.ProjectPath.Trim(),
                ProjectType = project.ProjectType.Trim(),
                Reason = project.Reason.Trim(),
            })],
            cancellationToken).ConfigureAwait(false);

        return new AddScanProjectsResult
        {
            ScanProjectIds = [.. storedProjects.Select(project => project.ScanProjectId)],
        };
    }

    public ValueTask<bool> DeleteScanProjectAsync(DeleteScanProjectArgs args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ScanProjectId);
        return _scanProjectStore.DeleteAsync(args.ScanProjectId.Trim(), cancellationToken);
    }

    public ValueTask<IReadOnlyList<StoredScanProject>> ListScanProjectsAsync(CancellationToken cancellationToken)
        =>
        _scanProjectStore.ListAsync(cancellationToken);

    public ValueTask<bool> SubmitReviewVerdictAsync(
        SubmitReviewVerdictArgs args,
        CancellationToken _)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Message);
        _verdictBuffer.Submit(args.Approved, args.Message.Trim());
        return ValueTask.FromResult(true);
    }

    private static void ValidateAddScanProjectArgs(AddScanProjectArgs args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ProjectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ProjectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ProjectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.Reason);
    }
}
