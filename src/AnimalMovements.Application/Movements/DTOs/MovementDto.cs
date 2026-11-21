using AnimalMovements.Domain.Enums;

namespace AnimalMovements.Application.Movements.DTOs;

public sealed record MovementDto(
    Guid MovementId,
    string AnimalId,
    Species Species,
    string FromLocationId,
    string ToLocationId,
    DateOnly MovementDate,
    string RecordedBy,
    string? Notes,
    DateTimeOffset CreatedAtUtc);
