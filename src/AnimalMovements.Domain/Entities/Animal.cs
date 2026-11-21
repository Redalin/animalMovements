using AnimalMovements.Domain.Common;
using AnimalMovements.Domain.Enums;
using AnimalMovements.Domain.Events;
using AnimalMovements.Domain.Exceptions;
using AnimalMovements.Domain.ValueObjects;

namespace AnimalMovements.Domain.Entities;

public sealed class Animal : AggregateRoot
{
    private readonly List<AnimalMovement> _movements = [];

    public AnimalId Id { get; private set; } = null!;
    public Species Species { get; private set; }
    public int Version { get; private set; }
    public IReadOnlyCollection<AnimalMovement> Movements => _movements.AsReadOnly();

    private Animal()
    {
    }

    private Animal(AnimalId id, Species species)
    {
        Id = id;
        Species = species;
        Version = 0;
    }

    public static Animal Create(AnimalId id, Species species)
    {
        if (!Enum.IsDefined(species))
        {
            throw new DomainValidationException(
                "INVALID_SPECIES",
                "Species must be Cattle or Deer.",
                "species");
        }

        return new Animal(id, species);
    }

    public AnimalMovement RecordMovement(
        Species species,
        LocationId fromLocationId,
        LocationId toLocationId,
        DateOnly movementDate,
        string recordedBy,
        string? notes,
        DateOnly nzToday,
        DateTimeOffset utcNow)
    {
        if (!Enum.IsDefined(species))
        {
            throw new DomainValidationException(
                "INVALID_SPECIES",
                "Species must be Cattle or Deer.",
                "species");
        }

        if (species != Species)
        {
            throw new DomainValidationException(
                "SPECIES_CANNOT_CHANGE",
                $"Species cannot change for animal '{Id.Value}'. Expected '{Species}' but received '{species}'.",
                "species");
        }

        if (fromLocationId == toLocationId)
        {
            throw new DomainValidationException(
                "SAME_LOCATION",
                "FromLocationId and ToLocationId must be different.",
                "toLocationId");
        }

        if (movementDate > nzToday)
        {
            throw new DomainValidationException(
                "FUTURE_MOVEMENT_DATE",
                $"MovementDate ({movementDate:yyyy-MM-dd}) cannot be in the future (NZ date: {nzToday:yyyy-MM-dd}).",
                "movementDate");
        }

        var lastMovement = _movements
            .OrderBy(m => m.MovementDate)
            .ThenBy(m => m.CreatedAtUtc)
            .LastOrDefault();

        if (lastMovement is not null)
        {
            if (fromLocationId != lastMovement.ToLocationId)
            {
                throw new DomainValidationException(
                    "LOCATION_CONTINUITY_MISMATCH",
                    $"FromLocationId ('{fromLocationId.Value}') must match the previous movement's ToLocationId ('{lastMovement.ToLocationId.Value}').",
                    "fromLocationId");
            }

            if (movementDate < lastMovement.MovementDate)
            {
                throw new DomainValidationException(
                    "MOVEMENT_DATE_EARLIER_THAN_PREVIOUS",
                    $"MovementDate ({movementDate:yyyy-MM-dd}) cannot be earlier than the previous movement date ({lastMovement.MovementDate:yyyy-MM-dd}).",
                    "movementDate");
            }

            if (movementDate == lastMovement.MovementDate && utcNow <= lastMovement.CreatedAtUtc)
            {
                throw new DomainValidationException(
                    "MOVEMENT_TIME_NOT_AFTER_PREVIOUS",
                    "A movement on the same date as the previous movement must be recorded at a later time.",
                    "movementDate");
            }
        }

        var movement = new AnimalMovement(
            Guid.CreateVersion7(),
            Id,
            fromLocationId,
            toLocationId,
            movementDate,
            recordedBy,
            notes,
            utcNow);

        _movements.Add(movement);
        Version++;

        RaiseDomainEvent(new AnimalMovementRecordedDomainEvent(
            Guid.CreateVersion7(),
            movement.MovementId,
            Id.Value,
            Species,
            movement.FromLocationId.Value,
            movement.ToLocationId.Value,
            movement.MovementDate,
            movement.RecordedBy,
            movement.Notes,
            movement.CreatedAtUtc));

        return movement;
    }

    public IReadOnlyList<AnimalMovement> GetOrderedMovements() =>
        _movements
            .OrderBy(m => m.MovementDate)
            .ThenBy(m => m.CreatedAtUtc)
            .ToList()
            .AsReadOnly();
}
