using System.Text.Json;
using System.Text.Json.Serialization;
using AnimalMovements.Domain.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AnimalMovements.Infrastructure.Persistence.Outbox;

public sealed class OutboxSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is AnimalMovementsDbContext dbContext)
        {
            var aggregates = dbContext.ChangeTracker
                .Entries<AggregateRoot>()
                .Where(e => e.Entity.DomainEvents.Count > 0)
                .Select(e => e.Entity)
                .ToList();

            var outboxMessages = new List<OutboxMessage>();

            foreach (var aggregate in aggregates)
            {
                foreach (var domainEvent in aggregate.DomainEvents)
                {
                    outboxMessages.Add(new OutboxMessage
                    {
                        Id = domainEvent.EventId,
                        EventType = domainEvent.GetType().Name,
                        PayloadJson = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions),
                        OccurredAtUtc = domainEvent.OccurredAtUtc,
                        Status = OutboxStatus.Pending,
                        RetryCount = 0
                    });
                }

                aggregate.ClearDomainEvents();
            }

            if (outboxMessages.Count > 0)
            {
                dbContext.OutboxMessages.AddRange(outboxMessages);
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
