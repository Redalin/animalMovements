using AnimalMovements.Application.Abstractions;
using AnimalMovements.Domain.Abstractions;
using AnimalMovements.Infrastructure.Auth;
using AnimalMovements.Infrastructure.Persistence;
using AnimalMovements.Infrastructure.Persistence.Outbox;
using AnimalMovements.Infrastructure.Persistence.Repositories;
using AnimalMovements.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AnimalMovements.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

        services.AddSingleton<INzClock, NzSystemClock>();
        services.AddSingleton<OutboxSaveChangesInterceptor>();

        var dbProvider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AnimalMovementsDbContext>((sp, options) =>
        {
            var outboxInterceptor = sp.GetRequiredService<OutboxSaveChangesInterceptor>();

            if (string.Equals(dbProvider, "Postgres", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dbProvider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(connectionString ?? throw new InvalidOperationException("DefaultConnection is required for Postgres."));
            }
            else
            {
                var sqliteConn = connectionString ?? "Data Source=data/animalmovements.db";
                EnsureSqliteDirectoryExists(sqliteConn);
                options.UseSqlite(sqliteConn);
            }

            options.AddInterceptors(outboxInterceptor);
        });

        services.AddScoped<IAnimalRepository, AnimalRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AnimalMovementsDbContext>());
        services.AddScoped<ILocalIdentityService, LocalIdentityService>();
        services.AddSingleton<IIntegrationEventPublisher, LoggingIntegrationEventPublisher>();

        if (!string.Equals(configuration["Outbox:DisableBackgroundProcessor"], "true", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHostedService<OutboxProcessor>();
        }

        return services;
    }

    private static void EnsureSqliteDirectoryExists(string connectionString)
    {
        const string prefix = "Data Source=";
        var idx = connectionString.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return;
        }

        var pathPart = connectionString[(idx + prefix.Length)..].Split(';')[0].Trim();
        if (string.IsNullOrWhiteSpace(pathPart) || pathPart.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var dir = Path.GetDirectoryName(pathPart);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }
}
