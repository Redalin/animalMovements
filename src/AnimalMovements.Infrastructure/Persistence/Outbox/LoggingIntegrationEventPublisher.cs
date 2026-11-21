using AnimalMovements.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace AnimalMovements.Infrastructure.Persistence.Outbox;

public sealed class LoggingIntegrationEventPublisher(
    ILogger<LoggingIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    public Task PublishAsync(
        Guid outboxMessageId,
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Outbox published integration event {EventType} (MessageId: {OutboxMessageId}). Payload: {Payload}",
            eventType,
            outboxMessageId,
            payloadJson);

        return Task.CompletedTask;
    }
}
