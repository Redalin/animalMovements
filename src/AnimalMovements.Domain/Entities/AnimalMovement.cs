using AnimalMovements.Domain.Exceptions;
using AnimalMovements.Domain.ValueObjects;

namespace AnimalMovements.Domain.Entities;

public sealed class AnimalMovement
{
    public const int RecordedByMaxLength = 100;
    public const int NotesMaxLength = 500;

    public Guid MovementId { get; private set; }
    public AnimalId AnimalId { get; private set; } = null!;
    public LocationId FromLocationId { get; private set; } = null!;
    public LocationId ToLocationId { get; private set; } = null!;
    public DateOnly MovementDate { get; private set; }
    public string RecordedBy { get; private set; } = null!;
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private AnimalMovement()
    {
    }

    internal AnimalMovement(
        Guid movementId,
        AnimalId animalId,
        LocationId fromLocationId,
        LocationId toLocationId,
        DateOnly movementDate,
        string recordedBy,
        string? notes,
        DateTimeOffset createdAtUtc)
    {
        MovementId = movementId;
        AnimalId = animalId;
        FromLocationId = fromLocationId;
        ToLocationId = toLocationId;
        MovementDate = movementDate;
        RecordedBy = NormalizeRecordedBy(recordedBy);
        Notes = NormalizeNotes(notes);
        CreatedAtUtc = createdAtUtc;
    }

    private static string NormalizeRecordedBy(string? rawRecordedBy)
    {
        var value = rawRecordedBy?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException(
                "RECORDED_BY_REQUIRED",
                "RecordedBy is required.",
                "recordedBy");
        }

        if (value.Length > RecordedByMaxLength)
        {
            throw new DomainValidationException(
                "RECORDED_BY_TOO_LONG",
                $"RecordedBy must be at most {RecordedByMaxLength} characters.",
                "recordedBy");
        }

        return value;
    }

    private static string? NormalizeNotes(string? rawNotes)
    {
        if (string.IsNullOrWhiteSpace(rawNotes))
        {
            return null;
        }

        var value = rawNotes.Trim();
        if (value.Length > NotesMaxLength)
        {
            throw new DomainValidationException(
                "NOTES_TOO_LONG",
                $"Notes must be at most {NotesMaxLength} characters.",
                "notes");
        }

        return value;
    }
}
