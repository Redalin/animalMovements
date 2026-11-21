using AnimalMovements.Domain.Entities;
using AnimalMovements.Domain.ValueObjects;

namespace AnimalMovements.Domain.Abstractions;

public interface IAnimalRepository
{
    Task<Animal?> GetByIdWithMovementsAsync(AnimalId animalId, CancellationToken cancellationToken = default);
    Task AddAsync(Animal animal, CancellationToken cancellationToken = default);
}
