using AnimalMovements.Application.Movements.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AnimalMovements.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IMovementService, MovementService>();
        return services;
    }
}
