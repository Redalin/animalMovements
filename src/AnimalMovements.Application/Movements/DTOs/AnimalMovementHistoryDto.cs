using AnimalMovements.Domain.Enums;

namespace AnimalMovements.Application.Movements.DTOs;

public sealed record AnimalMovementHistoryDto(
    string AnimalId,
    Species? Species,
    int TotalMovements,
    IReadOnlyList<MovementDto> Movements);
