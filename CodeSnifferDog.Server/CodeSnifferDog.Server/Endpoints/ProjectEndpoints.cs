using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectReports;
using CodeSnifferDog.Server.Services.ProjectReports.Export;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using CodeSnifferDog.Server.Shared.Reports;
using Microsoft.AspNetCore.Mvc;

namespace CodeSnifferDog.Server.Endpoints;

public static class ProjectEndpoints
{
    private const long UploadRequestBodyLimitBytes = 3L * 1024 * 1024 * 1024;

    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/projects");

        group.MapPost("/", UploadProjectAsync)
            .DisableAntiforgery()
            .WithMetadata(
                new RequestSizeLimitAttribute(UploadRequestBodyLimitBytes),
                new RequestFormLimitsAttribute
                {
                    MultipartBodyLengthLimit = UploadRequestBodyLimitBytes,
                })
            .WithName("UploadProject");

        group.MapGet("/", ListProjectsAsync)
            .WithName("ListProjects");

        group.MapGet("/sidebar", GetProjectSidebarSnapshotAsync)
            .WithName("GetProjectSidebarSnapshot");

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

    private static async Task<IResult> GetProjectSidebarSnapshotAsync(
        Guid? selectedProjectId,
        Services.Projects.Sidebar.ISnapshotService projectSidebarSnapshotService,
        CancellationToken cancellationToken)
    {
        ProjectSidebarSnapshotDto snapshot = await projectSidebarSnapshotService.GetSnapshotAsync(selectedProjectId, cancellationToken);
        return Results.Ok(snapshot);
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
        IReportService projectReportService,
        CancellationToken cancellationToken)
    {
        ProjectReportListDto? reports = await projectReportService.GetProjectReportListAsync(projectId, cancellationToken);
        return reports is null ? Results.NotFound() : Results.Ok(reports);
    }

    private static async Task<IResult> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        IReportService projectReportService,
        CancellationToken cancellationToken)
    {
        ProjectReportContentDto? report = await projectReportService.GetProjectReportAsync(projectId, reportId, cancellationToken);
        return report is null ? Results.NotFound() : Results.Ok(report);
    }

    private static async Task<IResult> GetProjectAgentStatusSnapshotAsync(
        Guid projectId,
        Guid? selectedAgentId,
        ISnapshotService projectAgentStatusSnapshotService,
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
        ISnapshotService projectAgentStatusSnapshotService,
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
        IReportService projectReportService,
        IExportService projectReportExportService,
        CancellationToken cancellationToken)
    {
        ProjectReportContentDto? report = await projectReportService.GetProjectReportAsync(projectId, reportId, cancellationToken);
        if (report is null)
            return Results.NotFound();

        ExportFile export = projectReportExportService.CreateMarkdown(report);
        return Results.File(export.Bytes, export.ContentType, export.FileName);
    }

    private static async Task<IResult> DownloadProjectReportBundleAsync(
        Guid projectId,
        IReportService projectReportService,
        IExportService projectReportExportService,
        CancellationToken cancellationToken)
    {
        ProjectReportBundleDto? bundle = await projectReportService.GetProjectReportBundleAsync(projectId, cancellationToken);
        if (bundle is null)
            return Results.NotFound();

        ExportFile export = await projectReportExportService.CreateBundleZipAsync(bundle, cancellationToken);
        return Results.File(export.Bytes, export.ContentType, export.FileName);
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
