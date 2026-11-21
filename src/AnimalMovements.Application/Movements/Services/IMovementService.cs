using AnimalMovements.Application.Movements.DTOs;

namespace AnimalMovements.Application.Movements.Services;

public interface IMovementService
{
    Task<MovementDto> RecordMovementAsync(RecordMovementRequest request, CancellationToken cancellationToken = default);
    Task<AnimalMovementHistoryDto> GetAnimalMovementHistoryAsync(string animalId, CancellationToken cancellationToken = default);
}
