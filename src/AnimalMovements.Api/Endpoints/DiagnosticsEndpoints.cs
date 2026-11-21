using AnimalMovements.Api.Services;
using AnimalMovements.Domain.Abstractions;
using AnimalMovements.Infrastructure.Auth;
using AnimalMovements.Infrastructure.Logging;
using AnimalMovements.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AnimalMovements.Api.Endpoints;

public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/diagnostics")
            .WithTags("Diagnostics & Observability");

        group.MapGet("/info", (
            IConfiguration configuration,
            IOptions<AuthOptions> authOptions,
            INzClock nzClock) =>
        {
            return Results.Ok(new
            {
                databaseProvider = configuration["Database:Provider"] ?? "Sqlite",
                authMode = authOptions.Value.Mode,
                nzToday = nzClock.NzToday,
                utcNow = nzClock.UtcNow,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            });
        })
        .AllowAnonymous()
        .WithSummary("Runtime environment, DB provider, auth mode, and NZ clock diagnostics");

        group.MapGet("/logs", (
            InMemoryLogStore logStore,
            string? level,
            string? search,
            int? take) =>
        {
            var entries = logStore.Query(level, search, take ?? 200);
            return Results.Ok(new { count = entries.Count, entries });
        })
        .AllowAnonymous()
        .WithSummary("Query recent structured verbose logs");

        group.MapDelete("/logs", (InMemoryLogStore logStore) =>
        {
            logStore.Clear();
            return Results.NoContent();
        })
        .AllowAnonymous()
        .WithSummary("Clear in-memory log buffer");

        group.MapGet("/outbox", async (
            AnimalMovementsDbContext dbContext,
            int? take,
            CancellationToken cancellationToken) =>
        {
            var clampedTake = Math.Clamp(take ?? 50, 1, 200);
            var messages = await dbContext.OutboxMessages
                .AsNoTracking()
                .OrderByDescending(m => m.OccurredAtUtc)
                .Take(clampedTake)
                .Select(m => new
                {
                    m.Id,
                    m.EventType,
                    status = m.Status.ToString(),
                    m.OccurredAtUtc,
                    m.ProcessedAtUtc,
                    m.RetryCount,
                    m.LastError,
                    m.PayloadJson
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new { count = messages.Count, messages });
        })
        .AllowAnonymous()
        .WithSummary("Inspect Outbox messages and eventual consistency delivery status");

        group.MapPost("/run-tests", (BusinessRuleTestRunner runner) =>
        {
            var result = runner.RunDomainRuleSuite();
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithSummary("Run automated business-rule verification suite");

        return app;
    }
}
