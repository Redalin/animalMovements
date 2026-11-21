namespace AnimalMovements.Domain.Abstractions;

public interface INzClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly NzToday { get; }
}
