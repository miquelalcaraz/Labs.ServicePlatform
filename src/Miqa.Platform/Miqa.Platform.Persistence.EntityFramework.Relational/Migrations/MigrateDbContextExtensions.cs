using System.Diagnostics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Miqa.Platform.Activities;

namespace Miqa.Platform.Persistence.Migrations;

public static class MigrateDbContextExtensions
{
    public static IServiceCollection AddMigration<TContext>(this IServiceCollection services)
        where TContext : DbContext
        => services.AddMigration<TContext>((_, _) => Task.CompletedTask);

    public static IServiceCollection AddMigration<TContext>(this IServiceCollection services, Func<TContext, IServiceProvider, Task> seeder)
        where TContext : DbContext
    {
        services.TryAddSingleton<MigrationReadySignal>();
        return services.AddHostedService(sp => new MigrationHostedService<TContext>(sp, seeder));
    }

    public static IServiceCollection AddMigration<TContext, TDbSeeder>(this IServiceCollection services)
        where TContext : DbContext
        where TDbSeeder : class, IDbSeeder<TContext>
    {
        services.AddScoped<IDbSeeder<TContext>, TDbSeeder>();
        return services.AddMigration<TContext>((context, sp) => sp.GetRequiredService<IDbSeeder<TContext>>().SeedAsync(context));
    }

    private static async Task MigrateDbContextAsync<TContext>(this IServiceProvider services, Func<TContext, IServiceProvider, Task> seeder) where TContext : DbContext
    {
        using var scope = services.CreateScope();
        var scopeServices = scope.ServiceProvider;
        var logger = scopeServices.GetRequiredService<ILogger<TContext>>();
        var context = scopeServices.GetRequiredService<TContext>();

        var contextName = typeof(TContext).Name;
        using var activity = MigrationHelper.StartMigrationOperation(contextName);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Migrating database associated with context {DbContextName}", contextName);

            var strategy = context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(() => InvokeSeeder(seeder, context, scopeServices));

            stopwatch.Stop();
            var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

            MigrationHelper.RecordMigrationSuccess(activity, contextName, "full_migration", elapsedMs);

            logger.LogInformation("Successfully migrated database for context {DbContextName} in {ElapsedMs}ms", contextName, elapsedMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            logger.LogError(ex, "An error occurred while migrating the database used on context {DbContextName}", contextName);

            MigrationHelper.RecordMigrationFailure(activity, contextName, "full_migration", ex);

            throw;
        }
    }

    private static async Task InvokeSeeder<TContext>(Func<TContext, IServiceProvider, Task> seeder, TContext context, IServiceProvider services)
        where TContext : DbContext
    {
        var contextName = typeof(TContext).Name;
        var stopwatch = Stopwatch.StartNew();

        // Schema migration
        using (var migrationActivity = MigrationHelper.StartMigration(contextName))
        {
            try
            {
                await context.Database.MigrateAsync();

                stopwatch.Stop();
                var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

                MigrationHelper.RecordMigrationSuccess(migrationActivity, contextName, "schema_migration", elapsedMs);
            }
            catch (Exception ex)
            {
                MigrationHelper.RecordMigrationFailure(migrationActivity, contextName, "schema_migration", ex);
                throw;
            }
        }

        // Seeding
        stopwatch.Restart();
        using (var seedingActivity = MigrationHelper.StartSeeding(contextName))
        {
            try
            {
                await seeder(context, services);

                stopwatch.Stop();
                var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

                MigrationHelper.RecordMigrationSuccess(seedingActivity, contextName, "seeding", elapsedMs);
            }
            catch (Exception ex)
            {
                MigrationHelper.RecordMigrationFailure(seedingActivity, contextName, "seeding", ex);
                throw;
            }
        }
    }

    private class MigrationHostedService<TContext>(IServiceProvider serviceProvider, Func<TContext, IServiceProvider, Task> seeder)
        : BackgroundService where TContext : DbContext
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await serviceProvider.MigrateDbContextAsync(seeder);
            serviceProvider.GetRequiredService<MigrationReadySignal>().Signal();
        }
    }
}
public interface IDbSeeder<in TContext> where TContext : DbContext
{
    Task SeedAsync(TContext context);
}
