using System.Diagnostics;
using AnimalMovements.Domain.Entities;
using AnimalMovements.Domain.Enums;
using AnimalMovements.Domain.Exceptions;
using AnimalMovements.Domain.ValueObjects;

namespace AnimalMovements.Api.Services;

public sealed record TestCaseResult(
    string Name,
    string Category,
    bool Passed,
    double DurationMs,
    string Detail);

public sealed record TestSuiteRunResult(
    string RunnerMode,
    int Total,
    int Passed,
    int Failed,
    double TotalDurationMs,
    DateTimeOffset ExecutedAtUtc,
    IReadOnlyList<TestCaseResult> Results);

public sealed class BusinessRuleTestRunner(ILogger<BusinessRuleTestRunner> logger)
{
    public TestSuiteRunResult RunDomainRuleSuite()
    {
        logger.LogInformation("Executing automated domain business-rule test suite.");
        var suiteWatch = Stopwatch.StartNew();
        var results = new List<TestCaseResult>();

        var nzToday = new DateOnly(2026, 11, 21);
        var baseUtc = new DateTimeOffset(2026, 11, 20, 22, 0, 0, TimeSpan.Zero);

        void RunCase(string name, string category, Action test)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                test();
                sw.Stop();
                results.Add(new TestCaseResult(name, category, true, Math.Round(sw.Elapsed.TotalMilliseconds, 2), "Passed"));
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new TestCaseResult(name, category, false, Math.Round(sw.Elapsed.TotalMilliseconds, 2), ex.Message));
            }
        }

        void ExpectDomainError(Action action, string expectedRuleCode)
        {
            try
            {
                action();
            }
            catch (DomainValidationException ex) when (ex.RuleCode == expectedRuleCode)
            {
                return;
            }
            catch (DomainValidationException ex)
            {
                throw new InvalidOperationException($"Expected rule '{expectedRuleCode}' but got '{ex.RuleCode}': {ex.Message}");
            }

            throw new InvalidOperationException($"Expected DomainValidationException '{expectedRuleCode}' but no exception was thrown.");
        }

        RunCase("AnimalId accepts exactly 15 numeric digits", "AnimalId", () =>
        {
            var id = AnimalId.Create("982000123456789");
            if (id.Value != "982000123456789") throw new InvalidOperationException("Unexpected value.");
        });

        RunCase("AnimalId rejects fewer than 15 digits", "AnimalId", () =>
            ExpectDomainError(() => AnimalId.Create("12345678901234"), "INVALID_ANIMAL_ID"));

        RunCase("AnimalId rejects non-numeric characters", "AnimalId", () =>
            ExpectDomainError(() => AnimalId.Create("98200012345678A"), "INVALID_ANIMAL_ID"));

        RunCase("FromLocation and ToLocation must be different", "Locations", () =>
        {
            var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Cattle);
            ExpectDomainError(() => animal.RecordMovement(
                Species.Cattle,
                LocationId.Create("FARM-001", "fromLocationId"),
                LocationId.Create("farm-001", "toLocationId"),
                nzToday,
                "Jane Doe",
                null,
                nzToday,
                baseUtc), "SAME_LOCATION");
        });

        RunCase("MovementDate cannot be in the future (NZ Time)", "Dates (NZ Time)", () =>
        {
            var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Deer);
            ExpectDomainError(() => animal.RecordMovement(
                Species.Deer,
                LocationId.Create("FARM-001", "fromLocationId"),
                LocationId.Create("FARM-002", "toLocationId"),
                nzToday.AddDays(1),
                "Jane Doe",
                null,
                nzToday,
                baseUtc), "FUTURE_MOVEMENT_DATE");
        });

        RunCase("FromLocation must match previous ToLocation", "Location Continuity", () =>
        {
            var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Cattle);
            animal.RecordMovement(
                Species.Cattle,
                LocationId.Create("FARM-A", "fromLocationId"),
                LocationId.Create("FARM-B", "toLocationId"),
                nzToday.AddDays(-2),
                "Jane Doe",
                null,
                nzToday,
                baseUtc);

            ExpectDomainError(() => animal.RecordMovement(
                Species.Cattle,
                LocationId.Create("FARM-C", "fromLocationId"),
                LocationId.Create("FARM-D", "toLocationId"),
                nzToday.AddDays(-1),
                "Jane Doe",
                null,
                nzToday,
                baseUtc.AddHours(1)), "LOCATION_CONTINUITY_MISMATCH");
        });

        RunCase("MovementDate cannot be earlier than previous movement", "Chronology", () =>
        {
            var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Cattle);
            animal.RecordMovement(
                Species.Cattle,
                LocationId.Create("FARM-A", "fromLocationId"),
                LocationId.Create("FARM-B", "toLocationId"),
                nzToday.AddDays(-1),
                "Jane Doe",
                null,
                nzToday,
                baseUtc);

            ExpectDomainError(() => animal.RecordMovement(
                Species.Cattle,
                LocationId.Create("FARM-B", "fromLocationId"),
                LocationId.Create("FARM-C", "toLocationId"),
                nzToday.AddDays(-2),
                "Jane Doe",
                null,
                nzToday,
                baseUtc.AddMinutes(5)), "MOVEMENT_DATE_EARLIER_THAN_PREVIOUS");
        });

        RunCase("Same-date movement allowed when recorded at a later time", "Chronology", () =>
        {
            var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Deer);
            animal.RecordMovement(
                Species.Deer,
                LocationId.Create("FARM-A", "fromLocationId"),
                LocationId.Create("FARM-B", "toLocationId"),
                nzToday,
                "Jane Doe",
                "Morning leg",
                nzToday,
                baseUtc);

            animal.RecordMovement(
                Species.Deer,
                LocationId.Create("FARM-B", "fromLocationId"),
                LocationId.Create("SALEYARD-1", "toLocationId"),
                nzToday,
                "Jane Doe",
                "Afternoon leg",
                nzToday,
                baseUtc.AddMinutes(30));

            if (animal.Movements.Count != 2) throw new InvalidOperationException("Expected 2 movements.");
        });

        RunCase("Same-date movement rejected if time is not after previous", "Chronology", () =>
        {
            var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Deer);
            animal.RecordMovement(
                Species.Deer,
                LocationId.Create("FARM-A", "fromLocationId"),
                LocationId.Create("FARM-B", "toLocationId"),
                nzToday,
                "Jane Doe",
                null,
                nzToday,
                baseUtc);

            ExpectDomainError(() => animal.RecordMovement(
                Species.Deer,
                LocationId.Create("FARM-B", "fromLocationId"),
                LocationId.Create("FARM-C", "toLocationId"),
                nzToday,
                "Jane Doe",
                null,
                nzToday,
                baseUtc), "MOVEMENT_TIME_NOT_AFTER_PREVIOUS");
        });

        RunCase("Species cannot change across movements", "Species Immutability", () =>
        {
            var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Cattle);
            animal.RecordMovement(
                Species.Cattle,
                LocationId.Create("FARM-A", "fromLocationId"),
                LocationId.Create("FARM-B", "toLocationId"),
                nzToday.AddDays(-1),
                "Jane Doe",
                null,
                nzToday,
                baseUtc);

            ExpectDomainError(() => animal.RecordMovement(
                Species.Deer,
                LocationId.Create("FARM-B", "fromLocationId"),
                LocationId.Create("FARM-C", "toLocationId"),
                nzToday,
                "Jane Doe",
                null,
                nzToday,
                baseUtc.AddHours(1)), "SPECIES_CANNOT_CHANGE");
        });

        RunCase("Recording a valid movement raises an Outbox-ready DomainEvent", "Domain Events / Outbox", () =>
        {
            var animal = Animal.Create(AnimalId.Create("982000123456789"), Species.Cattle);
            animal.RecordMovement(
                Species.Cattle,
                LocationId.Create("FARM-A", "fromLocationId"),
                LocationId.Create("FARM-B", "toLocationId"),
                nzToday,
                "Jane Doe",
                "Transported safely",
                nzToday,
                baseUtc);

            if (animal.DomainEvents.Count != 1)
                throw new InvalidOperationException($"Expected 1 domain event, found {animal.DomainEvents.Count}.");
        });

        suiteWatch.Stop();
        var passed = results.Count(r => r.Passed);
        var failed = results.Count - passed;

        logger.LogInformation(
            "Domain business-rule suite completed: {Passed}/{Total} passed in {DurationMs}ms.",
            passed,
            results.Count,
            Math.Round(suiteWatch.Elapsed.TotalMilliseconds, 2));

        return new TestSuiteRunResult(
            "In-Process Domain Rule Suite",
            results.Count,
            passed,
            failed,
            Math.Round(suiteWatch.Elapsed.TotalMilliseconds, 2),
            DateTimeOffset.UtcNow,
            results);
    }
}
