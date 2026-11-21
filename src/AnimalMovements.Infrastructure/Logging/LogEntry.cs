namespace AnimalMovements.Infrastructure.Logging;

public sealed record LogEntry(
    long Sequence,
    DateTimeOffset TimestampUtc,
    string Level,
    string Category,
    string Message,
    string? CorrelationId,
    string? Exception);
