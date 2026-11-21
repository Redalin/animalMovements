using AnimalMovements.Application.Movements.DTOs;
using AnimalMovements.Application.Movements.Services;

namespace AnimalMovements.Api.Endpoints;

public static class MovementEndpoints
{
    public static IEndpointRouteBuilder MapMovementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .WithTags("Movements")
            .RequireAuthorization();

        group.MapPost("/movements", async (
            RecordMovementRequest request,
            IMovementService movementService,
            CancellationToken cancellationToken) =>
        {
            var created = await movementService.RecordMovementAsync(request, cancellationToken);
            return Results.Created($"/api/animals/{created.AnimalId}/movements", created);
        })
        .WithName("RecordAnimalMovement")
        .WithSummary("Record a new animal movement")
        .WithDescription("Validates all business rules, persists the movement, and queues an Outbox integration event in the same transaction.")
        .Produces<MovementDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/animals/{animalId}/movements", async (
            string animalId,
            IMovementService movementService,
            CancellationToken cancellationToken) =>
        {
            var history = await movementService.GetAnimalMovementHistoryAsync(animalId, cancellationToken);
            return Results.Ok(history);
        })
        .WithName("GetAnimalMovementHistory")
        .WithSummary("Get movement history for an animal")
        .WithDescription("Returns the ordered movement history for the specified 15-digit AnimalId. Returns 200 OK with an empty list if no movements exist.")
        .Produces<AnimalMovementHistoryDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
