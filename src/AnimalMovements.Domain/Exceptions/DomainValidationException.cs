namespace AnimalMovements.Domain.Exceptions;

public sealed class DomainValidationException : Exception
{
    public string RuleCode { get; }
    public string? Field { get; }

    public DomainValidationException(string ruleCode, string message, string? field = null)
        : base(message)
    {
        RuleCode = ruleCode;
        Field = field;
    }
}
