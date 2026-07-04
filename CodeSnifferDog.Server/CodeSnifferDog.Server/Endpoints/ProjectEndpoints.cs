using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectReports;
using CodeSnifferDog.Server.Services.ProjectReports.Export;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using CodeSnifferDog.Server.Shared.Reports;
using Microsoft.AspNetCore.Mvc;

namespace CodeSnifferDog.Server.Endpoints;

/// <summary>
/// Maps HTTP endpoints for project intake, sidebar snapshots, agent-status snapshots, and report retrieval.
/// </summary>
public static class ProjectEndpoints
{
    /// <summary>
    /// Maximum multipart upload size accepted by the project upload endpoint.
    /// </summary>
    private const long UploadRequestBodyLimitBytes = 3L * 1024 * 1024 * 1024;

    /// <summary>
    /// Maps the project API endpoints under <c>/api/projects</c>.
    /// </summary>
    /// <param name="endpoints">Endpoint route builder that receives the mapped routes.</param>
    /// <returns>The original route builder.</returns>
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

    /// <summary>
    /// Uploads one project zip archive and creates a new project record.
    /// </summary>
    /// <param name="request">HTTP request whose multipart form contains the uploaded zip file in field <c>file</c>.</param>
    /// <param name="projectIntakeService">Service that stores the uploaded project and starts intake processing.</param>
    /// <param name="cancellationToken">Cancels form reading and upload processing.</param>
    /// <returns>A created response with the uploaded project result, or a bad-request response when input validation fails.</returns>
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

    /// <summary>
    /// Lists all projects available to the current client.
    /// </summary>
    /// <param name="projectIntakeService">Service that provides project summaries.</param>
    /// <param name="cancellationToken">Cancels project listing.</param>
    /// <returns>An OK response containing project list items.</returns>
    private static async Task<IResult> ListProjectsAsync(
        IProjectIntakeService projectIntakeService,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectListItemDto> projects = await projectIntakeService.ListAsync(cancellationToken);
        return Results.Ok(projects);
    }

    /// <summary>
    /// Returns the sidebar snapshot used by the client project list.
    /// </summary>
    /// <param name="selectedProjectId">Optional project identifier currently selected by the client.</param>
    /// <param name="projectSidebarSnapshotService">Service that builds the sidebar snapshot.</param>
    /// <param name="cancellationToken">Cancels snapshot loading.</param>
    /// <returns>An OK response containing the sidebar snapshot.</returns>
    private static async Task<IResult> GetProjectSidebarSnapshotAsync(
        Guid? selectedProjectId,
        Services.Projects.Sidebar.ISnapshotService projectSidebarSnapshotService,
        CancellationToken cancellationToken)
    {
        ProjectSidebarSnapshotDto snapshot = await projectSidebarSnapshotService.GetSnapshotAsync(selectedProjectId, cancellationToken);
        return Results.Ok(snapshot);
    }

    /// <summary>
    /// Returns the summary for one project.
    /// </summary>
    /// <param name="projectId">Project identifier to load.</param>
    /// <param name="projectIntakeService">Service that provides project summaries.</param>
    /// <param name="cancellationToken">Cancels project loading.</param>
    /// <returns>An OK response containing the project summary, or a not-found response when the project does not exist.</returns>
    private static async Task<IResult> GetProjectAsync(
        Guid projectId,
        IProjectIntakeService projectIntakeService,
        CancellationToken cancellationToken)
    {
        ProjectSummaryDto? project = await projectIntakeService.GetAsync(projectId, cancellationToken);
        return project is null ? Results.NotFound() : Results.Ok(project);
    }

    /// <summary>
    /// Returns the report list for one project.
    /// </summary>
    /// <param name="projectId">Project identifier whose reports should be listed.</param>
    /// <param name="projectReportService">Service that loads project reports.</param>
    /// <param name="cancellationToken">Cancels report-list loading.</param>
    /// <returns>An OK response containing the report list, or a not-found response when the project has no stored reports.</returns>
    private static async Task<IResult> GetProjectReportsAsync(
        Guid projectId,
        IReportService projectReportService,
        CancellationToken cancellationToken)
    {
        ListDto? reports = await projectReportService.GetProjectReportListAsync(projectId, cancellationToken);
        return reports is null ? Results.NotFound() : Results.Ok(reports);
    }

    /// <summary>
    /// Returns one report content payload for a project.
    /// </summary>
    /// <param name="projectId">Project identifier that owns the report.</param>
    /// <param name="reportId">Report identifier to load.</param>
    /// <param name="projectReportService">Service that loads project reports.</param>
    /// <param name="cancellationToken">Cancels report loading.</param>
    /// <returns>An OK response containing the report content, or a not-found response when the report does not exist.</returns>
    private static async Task<IResult> GetProjectReportAsync(
        Guid projectId,
        Guid reportId,
        IReportService projectReportService,
        CancellationToken cancellationToken)
    {
        ContentDto? report = await projectReportService.GetProjectReportAsync(projectId, reportId, cancellationToken);
        return report is null ? Results.NotFound() : Results.Ok(report);
    }

    /// <summary>
    /// Returns the agent-status snapshot for one project.
    /// </summary>
    /// <param name="projectId">Project identifier whose agent-status snapshot should be loaded.</param>
    /// <param name="selectedAgentId">Optional selected agent identifier whose history should be preloaded.</param>
    /// <param name="projectAgentStatusSnapshotService">Service that builds agent-status snapshots.</param>
    /// <param name="cancellationToken">Cancels snapshot loading.</param>
    /// <returns>An OK response containing the status snapshot, or a not-found response when the project is absent.</returns>
    private static async Task<IResult> GetProjectAgentStatusSnapshotAsync(
        Guid projectId,
        Guid? selectedAgentId,
        ISnapshotService projectAgentStatusSnapshotService,
        CancellationToken cancellationToken)
    {
        StatusSnapshotDto? snapshot = await projectAgentStatusSnapshotService.GetSnapshotAsync(
            projectId,
            selectedAgentId,
            cancellationToken);
        return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
    }

    /// <summary>
    /// Returns the timeline history for one agent inside one project.
    /// </summary>
    /// <param name="projectId">Project identifier that owns the agent.</param>
    /// <param name="agentId">Agent identifier whose history should be loaded.</param>
    /// <param name="projectAgentStatusSnapshotService">Service that loads agent history snapshots.</param>
    /// <param name="cancellationToken">Cancels history loading.</param>
    /// <returns>An OK response containing the agent history, or a not-found response when the agent history is unavailable.</returns>
    private static async Task<IResult> GetProjectAgentHistoryAsync(
        Guid projectId,
        Guid agentId,
        ISnapshotService projectAgentStatusSnapshotService,
        CancellationToken cancellationToken)
    {
        HistorySnapshotDto? history = await projectAgentStatusSnapshotService.GetAgentHistoryAsync(
            projectId,
            agentId,
            cancellationToken);
        return history is null ? Results.NotFound() : Results.Ok(history);
    }

    /// <summary>
    /// Downloads one stored report as a markdown file.
    /// </summary>
    /// <param name="projectId">Project identifier that owns the report.</param>
    /// <param name="reportId">Report identifier to export.</param>
    /// <param name="projectReportService">Service that loads report content.</param>
    /// <param name="projectReportExportService">Service that formats export files.</param>
    /// <param name="cancellationToken">Cancels report loading and export generation.</param>
    /// <returns>A file response containing the markdown report, or a not-found response when the report does not exist.</returns>
    private static async Task<IResult> DownloadProjectReportAsync(
        Guid projectId,
        Guid reportId,
        IReportService projectReportService,
        IExportService projectReportExportService,
        CancellationToken cancellationToken)
    {
        ContentDto? report = await projectReportService.GetProjectReportAsync(projectId, reportId, cancellationToken);
        if (report is null)
            return Results.NotFound();

        ExportFile export = projectReportExportService.CreateMarkdown(report);
        return Results.File(export.Bytes, export.ContentType, export.FileName);
    }

    /// <summary>
    /// Downloads all stored reports for one project as a zip archive.
    /// </summary>
    /// <param name="projectId">Project identifier whose report bundle should be exported.</param>
    /// <param name="projectReportService">Service that loads the report bundle.</param>
    /// <param name="projectReportExportService">Service that formats export files.</param>
    /// <param name="cancellationToken">Cancels bundle loading and zip creation.</param>
    /// <returns>A file response containing the report bundle zip, or a not-found response when no bundle exists.</returns>
    private static async Task<IResult> DownloadProjectReportBundleAsync(
        Guid projectId,
        IReportService projectReportService,
        IExportService projectReportExportService,
        CancellationToken cancellationToken)
    {
        BundleDto? bundle = await projectReportService.GetProjectReportBundleAsync(projectId, cancellationToken);
        if (bundle is null)
            return Results.NotFound();

        ExportFile export = await projectReportExportService.CreateBundleZipAsync(bundle, cancellationToken);
        return Results.File(export.Bytes, export.ContentType, export.FileName);
    }

    /// <summary>
    /// Deletes one project and its stored artifacts.
    /// </summary>
    /// <param name="projectId">Project identifier to delete.</param>
    /// <param name="projectIntakeService">Service that performs project deletion.</param>
    /// <param name="cancellationToken">Cancels project deletion.</param>
    /// <returns>A no-content response when deletion succeeds, a not-found response when the project is absent, or a bad-request response when deletion is rejected.</returns>
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

    /// <summary>
    /// Requests cancellation of one running project.
    /// </summary>
    /// <param name="projectId">Project identifier to cancel.</param>
    /// <param name="projectIntakeService">Service that performs project cancellation.</param>
    /// <param name="cancellationToken">Cancels the cancellation request itself.</param>
    /// <returns>A no-content response when cancellation succeeds, a not-found response when the project is absent, or a bad-request response when cancellation is rejected.</returns>
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
