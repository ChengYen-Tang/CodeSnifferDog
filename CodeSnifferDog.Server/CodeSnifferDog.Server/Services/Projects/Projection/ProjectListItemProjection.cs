using CodeSnifferDog.Server.Data.Entities;

namespace CodeSnifferDog.Server.Services.Projects.Projection;

internal sealed record ProjectListItemProjection(
    Guid ProjectId,
    string OriginalFileName,
    ProjectProcessingStatus Status,
    DateTimeOffset CreatedAtUtc);
