namespace AnimalMovements.Infrastructure.Auth;

public sealed record AuthTokenResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    string Email,
    string DisplayName,
    string Role);

public interface ILocalIdentityService
{
    Task<AuthTokenResponse> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken = default);
    Task<AuthTokenResponse?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task EnsureDemoUserSeededAsync(CancellationToken cancellationToken = default);
}
