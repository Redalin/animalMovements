namespace AnimalMovements.Application.Abstractions;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(
        Guid outboxMessageId,
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken = default);
}
