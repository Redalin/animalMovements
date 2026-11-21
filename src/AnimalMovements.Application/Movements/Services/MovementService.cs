using AnimalMovements.Application.Abstractions;
using AnimalMovements.Application.Exceptions;
using AnimalMovements.Application.Movements.DTOs;
using AnimalMovements.Domain.Abstractions;
using AnimalMovements.Domain.Entities;
using AnimalMovements.Domain.Exceptions;
using AnimalMovements.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AnimalMovements.Application.Movements.Services;

public sealed class MovementService(
    IAnimalRepository animalRepository,
    IUnitOfWork unitOfWork,
    INzClock nzClock,
    ICurrentUserService currentUserService,
    ILogger<MovementService> logger) : IMovementService
{
    public async Task<MovementDto> RecordMovementAsync(
        RecordMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidateRequiredInputPresence(request);

        logger.LogDebug(
            "Recording movement request for AnimalId {AnimalId}, Species {Species}, From {FromLocation} -> To {ToLocation} on {MovementDate} by {RecordedBy} (Actor: {Actor})",
            request.AnimalId,
            request.Species,
            request.FromLocationId,
            request.ToLocationId,
            request.MovementDate,
            request.RecordedBy,
            currentUserService.UserName ?? "anonymous");

        try
        {
            var animalId = AnimalId.Create(request.AnimalId);
            var fromLocation = LocationId.Create(request.FromLocationId, "fromLocationId");
            var toLocation = LocationId.Create(request.ToLocationId, "toLocationId");
            var species = request.Species!.Value;
            var movementDate = request.MovementDate!.Value;

            var animal = await animalRepository.GetByIdWithMovementsAsync(animalId, cancellationToken);
            var isNewAnimal = animal is null;

            if (animal is null)
            {
                logger.LogInformation(
                    "Animal {AnimalId} has no prior movements. Initializing new Animal aggregate with species {Species}.",
                    animalId.Value,
                    species);
                animal = Animal.Create(animalId, species);
            }

            var movement = animal.RecordMovement(
                species,
                fromLocation,
                toLocation,
                movementDate,
                request.RecordedBy!,
                request.Notes,
                nzClock.NzToday,
                nzClock.UtcNow);

            if (isNewAnimal)
            {
                await animalRepository.AddAsync(animal, cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Recorded movement {MovementId} for Animal {AnimalId} ({Species}): {FromLocation} -> {ToLocation} on {MovementDate}. Outbox event queued.",
                movement.MovementId,
                animal.Id.Value,
                animal.Species,
                movement.FromLocationId.Value,
                movement.ToLocationId.Value,
                movement.MovementDate);

            return new MovementDto(
                movement.MovementId,
                animal.Id.Value,
                animal.Species,
                movement.FromLocationId.Value,
                movement.ToLocationId.Value,
                movement.MovementDate,
                movement.RecordedBy,
                movement.Notes,
                movement.CreatedAtUtc);
        }
        catch (DomainValidationException ex)
        {
            logger.LogWarning(
                "Domain validation failed for AnimalId {AnimalId} with rule {RuleCode} on field {Field}: {Message}",
                request.AnimalId,
                ex.RuleCode,
                ex.Field ?? "(aggregate)",
                ex.Message);
            throw;
        }
    }

    public async Task<AnimalMovementHistoryDto> GetAnimalMovementHistoryAsync(
        string animalId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(animalId))
        {
            throw new BadInputException("AnimalId route parameter is required.");
        }

        var validatedId = AnimalId.Create(animalId);

        logger.LogDebug(
            "Retrieving movement history for Animal {AnimalId} (Actor: {Actor})",
            validatedId.Value,
            currentUserService.UserName ?? "anonymous");

        var animal = await animalRepository.GetByIdWithMovementsAsync(validatedId, cancellationToken);

        if (animal is null)
        {
            logger.LogInformation(
                "No movements recorded yet for Animal {AnimalId}. Returning empty movement history.",
                validatedId.Value);

            return new AnimalMovementHistoryDto(
                validatedId.Value,
                null,
                0,
                []);
        }

        var orderedMovements = animal
            .GetOrderedMovements()
            .Select(m => new MovementDto(
                m.MovementId,
                animal.Id.Value,
                animal.Species,
                m.FromLocationId.Value,
                m.ToLocationId.Value,
                m.MovementDate,
                m.RecordedBy,
                m.Notes,
                m.CreatedAtUtc))
            .ToList();

        logger.LogInformation(
            "Returned {Count} movement(s) for Animal {AnimalId} ({Species}).",
            orderedMovements.Count,
            animal.Id.Value,
            animal.Species);

        return new AnimalMovementHistoryDto(
            animal.Id.Value,
            animal.Species,
            orderedMovements.Count,
            orderedMovements);
    }

    private static void ValidateRequiredInputPresence(RecordMovementRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AnimalId))
            errors["animalId"] = ["animalId is required."];

        if (request.Species is null)
            errors["species"] = ["species is required (Cattle or Deer)."];

        if (string.IsNullOrWhiteSpace(request.FromLocationId))
            errors["fromLocationId"] = ["fromLocationId is required."];

        if (string.IsNullOrWhiteSpace(request.ToLocationId))
            errors["toLocationId"] = ["toLocationId is required."];

        if (request.MovementDate is null)
            errors["movementDate"] = ["movementDate is required (YYYY-MM-DD)."];

        if (string.IsNullOrWhiteSpace(request.RecordedBy))
            errors["recordedBy"] = ["recordedBy is required."];

        if (errors.Count > 0)
        {
            throw new BadInputException("One or more required fields are missing.", errors);
        }
    }
}
