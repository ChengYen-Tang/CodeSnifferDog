using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Data;

/// <summary>
/// Applies pending Entity Framework Core migrations during application startup.
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>
    /// Applies all pending database migrations.
    /// </summary>
    /// <param name="services">Service provider used to resolve logging and database services.</param>
    /// <param name="cancellationToken">Token that cancels the migration operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        ILogger logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("CodeSnifferDog.Server.Startup.DatabaseMigration");
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            string[] pendingMigrations = (await dbContext.Database
                .GetPendingMigrationsAsync(cancellationToken)
                .ConfigureAwait(false))
                .ToArray();

            if (pendingMigrations.Length == 0)
            {
                logger.LogInformation("Database schema is up to date. No pending EF Core migrations were found.");
                return;
            }

            logger.LogInformation(
                "Applying {MigrationCount} EF Core migration(s): {MigrationNames}",
                pendingMigrations.Length,
                string.Join(", ", pendingMigrations));

            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "EF Core database migration completed successfully. Applied through migration {LastMigration}.",
                pendingMigrations[^1]);
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "EF Core database migration failed during application startup.");
            throw;
        }
    }
}
