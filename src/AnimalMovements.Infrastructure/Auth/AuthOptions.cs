namespace AnimalMovements.Infrastructure.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// "Local" uses the built-in EF Core identity store and local JWT issuer.
    /// "External" delegates JWT validation to an external OIDC Authority (Entra ID, Auth0, Keycloak, etc.).
    /// </summary>
    public string Mode { get; set; } = "Local";

    public string Issuer { get; set; } = "AnimalMovements.Local";
    public string Audience { get; set; } = "AnimalMovements.Api";
    public string JwtSigningKey { get; set; } = "CHANGE_ME_DEV_ONLY_SUPER_SECRET_KEY_AT_LEAST_32_CHARS!";
    public int TokenExpiryMinutes { get; set; } = 120;

    /// <summary>
    /// External OIDC Identity Provider Authority URL (used when Mode == "External").
    /// </summary>
    public string? Authority { get; set; }

    public bool SeedDemoUser { get; set; } = true;
    public string DemoUserEmail { get; set; } = "demo@animalmovements.local";
    public string DemoUserPassword { get; set; } = "DemoPass123!";
    public string DemoUserDisplayName { get; set; } = "Demo Operator";

    public bool IsLocal => string.Equals(Mode, "Local", StringComparison.OrdinalIgnoreCase);
}
