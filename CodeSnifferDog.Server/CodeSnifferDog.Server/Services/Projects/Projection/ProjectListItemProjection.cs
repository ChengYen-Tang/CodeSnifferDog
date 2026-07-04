using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

/// <summary>
/// Read model used to project one project into a lightweight list item.
/// </summary>
/// <param name="ProjectId">Project identifier.</param>
/// <param name="OriginalFileName">Original uploaded file name.</param>
/// <param name="Status">Persisted processing status.</param>
/// <param name="CreatedAtUtc">Project creation timestamp in UTC.</param>
internal sealed record ProjectListItemProjection(
    Guid ProjectId,
    string OriginalFileName,
    ProjectProcessingStatus Status,
    DateTimeOffset CreatedAtUtc);
