namespace AnimalMovements.Infrastructure.Auth;

public sealed class LocalUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Operator";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
