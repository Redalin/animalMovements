using AnimalMovements.Domain.Common;
using AnimalMovements.Domain.Enums;

namespace AnimalMovements.Domain.Events;

public sealed record AnimalMovementRecordedDomainEvent(
    Guid EventId,
    Guid MovementId,
    string AnimalId,
    Species Species,
    string FromLocationId,
    string ToLocationId,
    DateOnly MovementDate,
    string RecordedBy,
    string? Notes,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
