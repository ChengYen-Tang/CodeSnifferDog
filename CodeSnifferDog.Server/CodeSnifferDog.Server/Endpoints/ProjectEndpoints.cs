using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.ProjectAgentSnapshots;
using CodeSnifferDog.Server.Services.ProjectReports;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using CodeSnifferDog.Server.Shared.Reports;
using System.IO.Compression;
using System.Text;

namespace CodeSnifferDog.Server.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/projects");

        group.MapPost("/", UploadProjectAsync)
            .DisableAntiforgery()
            .WithName("UploadProject");

        group.MapGet("/", ListProjectsAsync)
            .WithName("ListProjects");

        group.MapGet("/{projectId:guid}", GetProjectAsync)
            .WithName("GetProject");

        group.MapGet("/{projectId:guid}/reports", GetProjectReportsAsync)
            .WithName("GetProjectReports");

        group.MapGet("/{projectId:guid}/reports/{reportId:guid}", GetProjectReportAsync)
            .WithName("GetProjectReport");

        group.MapGet("/{projectId:guid}/agent-status", GetProjectAgentStatusSnapshotAsync)
            .WithName("GetProjectAgentStatusSnapshot");

        group.MapGet("/{projectId:guid}/agent-status/agents/{agentId:guid}/history", GetProjectAgentHistoryAsync)
            .WithName("GetProjectAgentHistory");

        group.MapGet("/{projectId:guid}/reports/{reportId:guid}/download", DownloadProjectReportAsync)
            .WithName("DownloadProjectReport");

        group.MapGet("/{projectId:guid}/reports/download", DownloadProjectReportBundleAsync)
            .WithName("DownloadProjectReportBundle");

        group.MapPost("/{projectId:guid}/cancel", CancelProjectAsync)
            .WithName("CancelProject");

        group.MapDelete("/{projectId:guid}", DeleteProjectAsync)
            .WithName("DeleteProject");

        return endpoints;
    }

    private static async Task<IResult> UploadProjectAsync(
        HttpRequest request,
        IProjectIntakeService projectIntakeService,
        CancellationToken cancellationToken)
    {
        IFormCollection form = await request.ReadFormAsync(cancellationToken);
        IFormFile? zipFile = form.Files["file"];
        if (zipFile is null)
            return Results.BadRequest(new { message = "A zip file is required in form field 'file'." });

        try
        {
            ProjectUploadResult result = await projectIntakeService.UploadAsync(zipFile, cancellationToken);
            return Results.Created($"/api/projects/{result.ProjectId}", result);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<IResult> ListProjectsAsync(
        IProjectIntakeService projectIntakeService,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectListItemDto> projects = await projectIntakeService.ListAsync(cancellationToken);
        return Results.Ok(projects);
    }

    private static async Task<IResult> GetProjectAsync(
        Guid projectId,
        IProjectIntakeService projectIntakeService,
        CancellationToken cancellationToken)
    {
        ProjectSummaryDto? project = await projectIntakeService.GetAsync(projectId, cancellationToken);
        return project is null ? Results.NotFound() : Results.Ok(project);
    }

    private static async Task<IResult> GetProjectReportsAsync(
        Guid projectId,
        IProjectReportService projectReportService,
        CancellationToken cancellationToken)
    {
        ProjectReportListDto? reports = await projectReportService.GetProjectReportListAsync(projectId, cancellationToken);
        return reports is null ? Results.NotFound() : Results.Ok(reports);
    }

    private static async Task<IResult> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        IProjectReportService projectReportService,
        CancellationToken cancellationToken)
    {
        ProjectReportContentDto? report = await projectReportService.GetProjectReportAsync(projectId, reportId, cancellationToken);
        return report is null ? Results.NotFound() : Results.Ok(report);
    }

    private static async Task<IResult> GetProjectAgentStatusSnapshotAsync(
        Guid projectId,
        Guid? selectedAgentId,
        IProjectAgentStatusSnapshotService projectAgentStatusSnapshotService,
        CancellationToken cancellationToken)
    {
        ProjectAgentStatusSnapshotDto? snapshot = await projectAgentStatusSnapshotService.GetSnapshotAsync(
            projectId,
            selectedAgentId,
            cancellationToken);
        return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
    }

    private static async Task<IResult> GetProjectAgentHistoryAsync(
        Guid projectId,
        Guid agentId,
        IProjectAgentStatusSnapshotService projectAgentStatusSnapshotService,
        CancellationToken cancellationToken)
    {
        ProjectAgentHistorySnapshotDto? history = await projectAgentStatusSnapshotService.GetAgentHistoryAsync(
            projectId,
            agentId,
            cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    }

    private static async Task<IResult> DownloadProjectReportAsync(
        Guid projectId,
        Guid reportId,
        IProjectReportService projectReportService,
        CancellationToken cancellationToken)
    {
        ProjectReportContentDto? report = await projectReportService.GetProjectReportAsync(projectId, reportId, cancellationToken);
        if (report is null)
            return Results.NotFound();

        byte[] bytes = Encoding.UTF8.GetBytes(report.MarkdownContent);
        return Results.File(bytes, "text/markdown; charset=utf-8", $"{report.RuleName}.md");
    }

    private static async Task<IResult> DownloadProjectReportBundleAsync(
        Guid projectId,
        IProjectReportService projectReportService,
        CancellationToken cancellationToken)
    {
        ProjectReportBundleDto? bundle = await projectReportService.GetProjectReportBundleAsync(projectId, cancellationToken);
        if (bundle is null)
            return Results.NotFound();

        using MemoryStream archiveStream = new();
        using (ZipArchive archive = new(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (ProjectRuleReportDto report in bundle.Reports)
            {
                ZipArchiveEntry entry = archive.CreateEntry($"{report.RuleName}.md", CompressionLevel.Fastest);
                await using Stream entryStream = entry.Open();
                await using StreamWriter writer = new(entryStream, Encoding.UTF8, leaveOpen: false);
                await writer.WriteAsync(report.MarkdownContent.AsMemory(), cancellationToken);
            }
        }

        return Results.File(
            archiveStream.ToArray(),
            "application/zip",
            $"{Path.GetFileNameWithoutExtension(bundle.OriginalFileName)}-reports.zip");
    }

    private static async Task<IResult> DeleteProjectAsync(
        Guid projectId,
        IProjectIntakeService projectIntakeService,
        CancellationToken cancellationToken)
    {
        try
        {
            bool deleted = await projectIntakeService.DeleteAsync(projectId, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<IResult> CancelProjectAsync(
        Guid projectId,
        IProjectIntakeService projectIntakeService,
        CancellationToken cancellationToken)
    {
        try
        {
            bool canceled = await projectIntakeService.CancelAsync(projectId, cancellationToken);
            return canceled ? Results.NoContent() : Results.NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }
}
