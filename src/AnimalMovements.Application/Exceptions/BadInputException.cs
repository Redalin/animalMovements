namespace AnimalMovements.Application.Exceptions;

public sealed class BadInputException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public BadInputException(string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }
}
