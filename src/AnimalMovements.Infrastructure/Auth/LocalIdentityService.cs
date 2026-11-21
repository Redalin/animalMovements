using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AnimalMovements.Application.Exceptions;
using AnimalMovements.Domain.Exceptions;
using AnimalMovements.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AnimalMovements.Infrastructure.Auth;

public sealed class LocalIdentityService(
    AnimalMovementsDbContext dbContext,
    IOptions<AuthOptions> authOptions,
    ILogger<LocalIdentityService> logger) : ILocalIdentityService
{
    private readonly PasswordHasher<LocalUser> _passwordHasher = new();
    private readonly AuthOptions _options = authOptions.Value;

    public async Task<AuthTokenResponse> RegisterAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(displayName))
        {
            throw new BadInputException("Email, password, and displayName are required.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (!normalizedEmail.Contains('@') || normalizedEmail.Length > 256)
        {
            throw new DomainValidationException("INVALID_EMAIL", "A valid email address is required.", "email");
        }

        if (password.Length < 8)
        {
            throw new DomainValidationException("WEAK_PASSWORD", "Password must be at least 8 characters.", "password");
        }

        var exists = await dbContext.LocalUsers.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (exists)
        {
            throw new DomainValidationException("EMAIL_ALREADY_REGISTERED", "An account with this email already exists.", "email");
        }

        var user = new LocalUser
        {
            Id = Guid.CreateVersion7(),
            Email = normalizedEmail,
            DisplayName = displayName.Trim(),
            Role = "Operator",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        dbContext.LocalUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Registered new local user {Email} ({UserId}).", user.Email, user.Id);
        return IssueToken(user);
    }

    public async Task<AuthTokenResponse?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await dbContext.LocalUsers.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("Failed login attempt for unknown account {Email}.", normalizedEmail);
            return null;
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            logger.LogWarning("Failed login attempt (invalid password) for account {Email}.", normalizedEmail);
            return null;
        }

        logger.LogInformation("Authenticated local user {Email} ({UserId}).", user.Email, user.Id);
        return IssueToken(user);
    }

    public async Task EnsureDemoUserSeededAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsLocal || !_options.SeedDemoUser)
        {
            return;
        }

        var normalizedEmail = _options.DemoUserEmail.Trim().ToLowerInvariant();
        var exists = await dbContext.LocalUsers.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (exists)
        {
            return;
        }

        var demoUser = new LocalUser
        {
            Id = Guid.CreateVersion7(),
            Email = normalizedEmail,
            DisplayName = _options.DemoUserDisplayName,
            Role = "Operator",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        demoUser.PasswordHash = _passwordHasher.HashPassword(demoUser, _options.DemoUserPassword);

        dbContext.LocalUsers.Add(demoUser);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded local demo user {Email}.", demoUser.Email);
    }

    private AuthTokenResponse IssueToken(LocalUser user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.TokenExpiryMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
        return new AuthTokenResponse(tokenValue, "Bearer", expiresAt, user.Email, user.DisplayName, user.Role);
    }
}
