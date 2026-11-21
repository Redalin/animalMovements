using AnimalMovements.Application.Abstractions;
using AnimalMovements.Application.Exceptions;
using AnimalMovements.Domain.Entities;
using AnimalMovements.Infrastructure.Auth;
using AnimalMovements.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AnimalMovements.Infrastructure.Persistence;

public sealed class AnimalMovementsDbContext(DbContextOptions<AnimalMovementsDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Animal> Animals => Set<Animal>();
    public DbSet<AnimalMovement> Movements => Set<AnimalMovement>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<LocalUser> LocalUsers => Set<LocalUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AnimalMovementsDbContext).Assembly);

        // Normalize DateTimeOffset to UTC DateTime so ORDER BY and comparisons work identically in SQLite and PostgreSQL.
        var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, DateTime>(
            v => v.UtcDateTime,
            v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)));

        var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, DateTime?>(
            v => v.HasValue ? v.Value.UtcDateTime : null,
            v => v.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(v.Value, DateTimeKind.Utc)) : null);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(dateTimeOffsetConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableDateTimeOffsetConverter);
                }
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "A concurrent update occurred for this animal. Please retry the operation.",
                ex);
        }
    }
}
