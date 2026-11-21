using AnimalMovements.Domain.Exceptions;

namespace AnimalMovements.Domain.ValueObjects;

public sealed record LocationId
{
    public const int MaxLength = 20;

    public string Value { get; }

    private LocationId(string value)
    {
        Value = value;
    }

    public static LocationId Create(string? rawValue, string fieldName)
    {
        var value = rawValue?.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException(
                "LOCATION_REQUIRED",
                $"{fieldName} is required.",
                fieldName);
        }

        if (value.Length > MaxLength)
        {
            throw new DomainValidationException(
                "LOCATION_TOO_LONG",
                $"{fieldName} must be at most {MaxLength} characters.",
                fieldName);
        }

        return new LocationId(value);
    }

    public bool Equals(LocationId? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;
}
