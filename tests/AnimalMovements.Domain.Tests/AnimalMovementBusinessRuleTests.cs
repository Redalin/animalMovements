using AnimalMovements.Domain.Entities;
using AnimalMovements.Domain.Enums;
using AnimalMovements.Domain.Events;
using AnimalMovements.Domain.Exceptions;
using AnimalMovements.Domain.ValueObjects;
using Xunit;

namespace AnimalMovements.Domain.Tests;

public sealed class AnimalMovementBusinessRuleTests
{
    private static readonly DateOnly NzToday = new(2026, 11, 21);
    private static readonly DateTimeOffset BaseUtc = new(2026, 11, 20, 22, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("982000123456789")]
    [InlineData("000000000000001")]
    public void AnimalId_Valid15NumericChars_Succeeds(string rawId)
    {
        var id = AnimalId.Create(rawId);
        Assert.Equal(rawId, id.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678901234")]      // 14 digits
    [InlineData("1234567890123456")]    // 16 digits
    [InlineData("98200012345678A")]     // non-numeric
    [InlineData("982000-23456789")]     // hyphen
    public void AnimalId_InvalidFormat_ThrowsDomainValidationException(string? rawId)
    {
        var ex = Assert.Throws<DomainValidationException>(() => AnimalId.Create(rawId));
        Assert.Equal("INVALID_ANIMAL_ID", ex.RuleCode);
    }

    [Fact]
    public void RecordMovement_SameFromAndToLocation_ThrowsDomainValidationException()
    {
        var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Cattle);

        var ex = Assert.Throws<DomainValidationException>(() => animal.RecordMovement(
            Species.Cattle,
            LocationId.Create("FARM-001", "fromLocationId"),
            LocationId.Create("farm-001", "toLocationId"),
            NzToday,
            "Jane Doe",
            null,
            NzToday,
            BaseUtc));

        Assert.Equal("SAME_LOCATION", ex.RuleCode);
    }

    [Fact]
    public void RecordMovement_FutureDateInNzTime_ThrowsDomainValidationException()
    {
        var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Deer);

        var ex = Assert.Throws<DomainValidationException>(() => animal.RecordMovement(
            Species.Deer,
            LocationId.Create("FARM-001", "fromLocationId"),
            LocationId.Create("FARM-002", "toLocationId"),
            NzToday.AddDays(1),
            "Jane Doe",
            null,
            NzToday,
            BaseUtc));

        Assert.Equal("FUTURE_MOVEMENT_DATE", ex.RuleCode);
    }

    [Fact]
    public void RecordMovement_FromLocationDoesNotMatchPreviousToLocation_ThrowsDomainValidationException()
    {
        var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Cattle);
        animal.RecordMovement(
            Species.Cattle,
            LocationId.Create("FARM-A", "fromLocationId"),
            LocationId.Create("FARM-B", "toLocationId"),
            NzToday.AddDays(-2),
            "Jane Doe",
            null,
            NzToday,
            BaseUtc);

        var ex = Assert.Throws<DomainValidationException>(() => animal.RecordMovement(
            Species.Cattle,
            LocationId.Create("FARM-X", "fromLocationId"),
            LocationId.Create("FARM-C", "toLocationId"),
            NzToday.AddDays(-1),
            "Jane Doe",
            null,
            NzToday,
            BaseUtc.AddHours(1)));

        Assert.Equal("LOCATION_CONTINUITY_MISMATCH", ex.RuleCode);
    }

    [Fact]
    public void RecordMovement_EarlierThanPreviousMovementDate_ThrowsDomainValidationException()
    {
        var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Cattle);
        animal.RecordMovement(
            Species.Cattle,
            LocationId.Create("FARM-A", "fromLocationId"),
            LocationId.Create("FARM-B", "toLocationId"),
            NzToday.AddDays(-1),
            "Jane Doe",
            null,
            NzToday,
            BaseUtc);

        var ex = Assert.Throws<DomainValidationException>(() => animal.RecordMovement(
            Species.Cattle,
            LocationId.Create("FARM-B", "fromLocationId"),
            LocationId.Create("FARM-C", "toLocationId"),
            NzToday.AddDays(-2),
            "Jane Doe",
            null,
            NzToday,
            BaseUtc.AddMinutes(10)));

        Assert.Equal("MOVEMENT_DATE_EARLIER_THAN_PREVIOUS", ex.RuleCode);
    }

    [Fact]
    public void RecordMovement_SameDateWithLaterTime_Succeeds()
    {
        var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Deer);

        var first = animal.RecordMovement(
            Species.Deer,
            LocationId.Create("FARM-A", "fromLocationId"),
            LocationId.Create("FARM-B", "toLocationId"),
            NzToday,
            "Jane Doe",
            "First leg",
            NzToday,
            BaseUtc);

        var second = animal.RecordMovement(
            Species.Deer,
            LocationId.Create("FARM-B", "fromLocationId"),
            LocationId.Create("SALEYARD-1", "toLocationId"),
            NzToday,
            "Jane Doe",
            "Second leg",
            NzToday,
            BaseUtc.AddSeconds(1));

        var ordered = animal.GetOrderedMovements();
        Assert.Equal(2, ordered.Count);
        Assert.Equal(first.MovementId, ordered[0].MovementId);
        Assert.Equal(second.MovementId, ordered[1].MovementId);
    }

    [Fact]
    public void RecordMovement_SameDateWithEarlierOrEqualTime_ThrowsDomainValidationException()
    {
        var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Deer);
        animal.RecordMovement(
            Species.Deer,
            LocationId.Create("FARM-A", "fromLocationId"),
            LocationId.Create("FARM-B", "toLocationId"),
            NzToday,
            "Jane Doe",
            null,
            NzToday,
            BaseUtc);

        var ex = Assert.Throws<DomainValidationException>(() => animal.RecordMovement(
            Species.Deer,
            LocationId.Create("FARM-B", "fromLocationId"),
            LocationId.Create("FARM-C", "toLocationId"),
            NzToday,
            "Jane Doe",
            null,
            NzToday,
            BaseUtc));

        Assert.Equal("MOVEMENT_TIME_NOT_AFTER_PREVIOUS", ex.RuleCode);
    }

    [Fact]
    public void RecordMovement_SpeciesChange_ThrowsDomainValidationException()
    {
        var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Cattle);
        animal.RecordMovement(
            Species.Cattle,
            LocationId.Create("FARM-A", "fromLocationId"),
            LocationId.Create("FARM-B", "toLocationId"),
            NzToday.AddDays(-1),
            "Jane Doe",
            null,
            NzToday,
            BaseUtc);

        var ex = Assert.Throws<DomainValidationException>(() => animal.RecordMovement(
            Species.Deer,
            LocationId.Create("FARM-B", "fromLocationId"),
            LocationId.Create("FARM-C", "toLocationId"),
            NzToday,
            "Jane Doe",
            null,
            NzToday,
            BaseUtc.AddHours(1)));

        Assert.Equal("SPECIES_CANNOT_CHANGE", ex.RuleCode);
    }

    [Fact]
    public void RecordMovement_FieldLengthConstraints_Enforced()
    {
        Assert.Throws<DomainValidationException>(() =>
            LocationId.Create(new string('A', 21), "fromLocationId"));

        var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Cattle);

        var recordedByEx = Assert.Throws<DomainValidationException>(() => animal.RecordMovement(
            Species.Cattle,
            LocationId.Create("FARM-A", "fromLocationId"),
            LocationId.Create("FARM-B", "toLocationId"),
            NzToday,
            new string('R', 101),
            null,
            NzToday,
            BaseUtc));
        Assert.Equal("RECORDED_BY_TOO_LONG", recordedByEx.RuleCode);

        var notesEx = Assert.Throws<DomainValidationException>(() => animal.RecordMovement(
            Species.Cattle,
            LocationId.Create("FARM-A", "fromLocationId"),
            LocationId.Create("FARM-B", "toLocationId"),
            NzToday,
            "Jane Doe",
            new string('N', 501),
            NzToday,
            BaseUtc));
        Assert.Equal("NOTES_TOO_LONG", notesEx.RuleCode);
    }

    [Fact]
    public void RecordMovement_ValidMovement_RaisesDomainEvent()
    {
        var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Cattle);

        var movement = animal.RecordMovement(
            Species.Cattle,
            LocationId.Create("FARM-A", "fromLocationId"),
            LocationId.Create("FARM-B", "toLocationId"),
            NzToday,
            "Jane Doe",
            "Healthy",
            NzToday,
            BaseUtc);

        var domainEvent = Assert.Single(animal.DomainEvents);
        var recorded = Assert.IsType<AnimalMovementRecordedDomainEvent>(domainEvent);
        Assert.Equal(movement.MovementId, recorded.MovementId);
        Assert.Equal("982000123456789", recorded.AnimalId);
        Assert.Equal(Species.Cattle, recorded.Species);
    }
}
