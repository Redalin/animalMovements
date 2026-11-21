using AnimalMovements.Domain.Abstractions;
using AnimalMovements.Domain.Entities;
using AnimalMovements.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AnimalMovements.Infrastructure.Persistence.Repositories;

public sealed class AnimalRepository(AnimalMovementsDbContext dbContext) : IAnimalRepository
{
    public async Task<Animal?> GetByIdWithMovementsAsync(
        AnimalId animalId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Animals
            .Include(a => a.Movements)
            .FirstOrDefaultAsync(a => a.Id == animalId, cancellationToken);
    }

    public async Task AddAsync(Animal animal, CancellationToken cancellationToken = default)
    {
        await dbContext.Animals.AddAsync(animal, cancellationToken);
    }
}
