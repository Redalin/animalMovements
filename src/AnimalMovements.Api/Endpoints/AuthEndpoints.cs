using AnimalMovements.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace AnimalMovements.Api.Endpoints;

public sealed record LoginRequest(string Email, string Password);
public sealed record RegisterRequest(string Email, string Password, string DisplayName);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, AuthOptions authOptions)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapGet("/config", (IOptions<AuthOptions> options) =>
        {
            var cfg = options.Value;
            return Results.Ok(new
            {
                mode = cfg.Mode,
                issuer = cfg.Issuer,
                authority = cfg.IsLocal ? null : cfg.Authority,
                demoUserSeeded = cfg.IsLocal && cfg.SeedDemoUser,
                demoEmail = cfg.IsLocal && cfg.SeedDemoUser ? cfg.DemoUserEmail : null
            });
        })
        .AllowAnonymous()
        .WithSummary("Get current authentication mode (Local vs External IdP)");

        if (!authOptions.IsLocal)
        {
            return app;
        }

        group.MapPost("/login", async (
            LoginRequest request,
            ILocalIdentityService identityService,
            CancellationToken cancellationToken) =>
        {
            var result = await identityService.LoginAsync(request.Email, request.Password, cancellationToken);
            if (result is null)
            {
                return Results.Problem(
                    title: "Unauthorized",
                    detail: "Invalid email or password.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithName("LoginLocalUser")
        .WithSummary("Sign in using the local identity store and receive a JWT Bearer token")
        .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/register", async (
            RegisterRequest request,
            ILocalIdentityService identityService,
            CancellationToken cancellationToken) =>
        {
            var result = await identityService.RegisterAsync(
                request.Email,
                request.Password,
                request.DisplayName,
                cancellationToken);

            return Results.Created("/api/auth/login", result);
        })
        .AllowAnonymous()
        .WithName("RegisterLocalUser")
        .WithSummary("Register a new local user and receive a JWT Bearer token")
        .Produces<AuthTokenResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
