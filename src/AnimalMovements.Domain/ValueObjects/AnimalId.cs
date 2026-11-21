using AnimalMovements.Domain.Exceptions;

namespace AnimalMovements.Domain.ValueObjects;

public sealed record AnimalId
{
    public const int ExactLength = 15;

    public string Value { get; }

    private AnimalId(string value)
    {
        Value = value;
    }

    public static AnimalId Create(string? rawValue)
    {
        var value = rawValue?.Trim();

        if (string.IsNullOrEmpty(value) || value.Length != ExactLength || !value.All(char.IsAsciiDigit))
        {
            throw new DomainValidationException(
                "INVALID_ANIMAL_ID",
                $"AnimalId must be exactly {ExactLength} numeric characters.",
                "animalId");
        }

        return new AnimalId(value);
    }

    public override string ToString() => Value;
}
