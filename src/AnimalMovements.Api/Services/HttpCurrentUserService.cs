using System.Security.Claims;
using AnimalMovements.Application.Abstractions;

namespace AnimalMovements.Api.Services;

public sealed class HttpCurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string? UserId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User?.FindFirstValue("sub");

    public string? UserName =>
        User?.FindFirstValue(ClaimTypes.Email)
        ?? User?.FindFirstValue(ClaimTypes.Name)
        ?? UserId;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
