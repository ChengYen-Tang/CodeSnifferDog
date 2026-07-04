using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectStorage;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectIntake.Deletion;

/// <summary>
/// Deletes projects from persistence and removes their temporary storage.
/// </summary>
/// <param name="dbContextFactory">Factory used to create database contexts for deletion.</param>
/// <param name="storagePaths">Path helper used to resolve stored zip and extracted project paths.</param>
internal sealed class DeletionService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    ProjectTemporaryStoragePaths storagePaths) : IDeletionService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly ProjectTemporaryStoragePaths _storagePaths = storagePaths;

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectRecord? project = await dbContext.Projects
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

        if (project is null)
            return false;

        if (project.Status == ProjectProcessingStatus.Reviewing)
            throw new InvalidOperationException("Reviewing projects must be canceled before deletion.");

        string uploadedZipPath = _storagePaths.ResolveStoredZipPath(project.StoredZipRelativePath);
        string extractedProjectPath = _storagePaths.ResolveExtractedProjectPath(project.Id);

        DeleteFileIfExists(uploadedZipPath);
        DeleteDirectoryIfExists(extractedProjectPath);

        dbContext.Projects.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Deletes one file when it exists.
    /// </summary>
    /// <param name="filePath">File path to delete.</param>
    private static void DeleteFileIfExists(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    /// <summary>
    /// Deletes one directory recursively when it exists.
    /// </summary>
    /// <param name="directoryPath">Directory path to delete.</param>
    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive: true);
    }
}
