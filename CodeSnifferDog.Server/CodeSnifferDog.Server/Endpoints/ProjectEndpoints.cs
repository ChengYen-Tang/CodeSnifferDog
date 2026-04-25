using CodeSnifferDog.Server.Services.ProjectIntake;

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
        IReadOnlyList<ProjectSummaryDto> projects = await projectIntakeService.ListAsync(cancellationToken);
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
}
