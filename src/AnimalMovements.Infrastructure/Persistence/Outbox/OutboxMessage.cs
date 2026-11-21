namespace AnimalMovements.Infrastructure.Persistence.Outbox;

public enum OutboxStatus
{
    Pending = 0,
    Processed = 1,
    DeadLettered = 2
}

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
