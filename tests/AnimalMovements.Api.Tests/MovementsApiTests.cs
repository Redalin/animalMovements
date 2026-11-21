using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AnimalMovements.Application.Movements.DTOs;
using AnimalMovements.Domain.Abstractions;
using AnimalMovements.Domain.Enums;
using AnimalMovements.Infrastructure.Auth;
using AnimalMovements.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AnimalMovements.Api.Tests;

public sealed class MovementsApiTests : IClassFixture<MovementsApiFactory>
{
    private readonly MovementsApiFactory _factory;
    private readonly HttpClient _client;

    public MovementsApiTests(MovementsApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Endpoints_WithoutJwt_Return401Unauthorized()
    {
        var getRes = await _client.GetAsync("/api/animals/982000123456789/movements");
        Assert.Equal(HttpStatusCode.Unauthorized, getRes.StatusCode);

        var postRes = await _client.PostAsJsonAsync("/api/movements", new
        {
            animalId = "982000123456789",
            species = "Cattle",
            fromLocationId = "FARM-1",
            toLocationId = "FARM-2",
            movementDate = "2026-11-21",
            recordedBy = "Jane Doe"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, postRes.StatusCode);
    }

    [Fact]
    public async Task GetHistory_UnknownAnimal_Returns200WithEmptyHistory()
    {
        var authedClient = await CreateAuthenticatedClientAsync();

        var response = await authedClient.GetAsync("/api/animals/999000000000001/movements");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await response.Content.ReadFromJsonAsync<AnimalMovementHistoryDto>(JsonOptions);
        Assert.NotNull(history);
        Assert.Equal("999000000000001", history.AnimalId);
        Assert.Null(history.Species);
        Assert.Equal(0, history.TotalMovements);
        Assert.Empty(history.Movements);
    }

    [Fact]
    public async Task PostMovement_MissingRequiredFields_Returns400BadRequest()
    {
        var authedClient = await CreateAuthenticatedClientAsync();

        var response = await authedClient.PostAsJsonAsync("/api/movements", new
        {
            animalId = "982000123456789",
            species = "Cattle",
            fromLocationId = "FARM-1",
            toLocationId = "FARM-2",
            movementDate = "2026-11-21"
            // recordedBy missing
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostMovement_ValidChain_CreatesOutboxAndReturnsOrderedHistory()
    {
        var authedClient = await CreateAuthenticatedClientAsync();
        const string animalId = "982000111222333";

        var firstResponse = await authedClient.PostAsJsonAsync("/api/movements", new
        {
            animalId,
            species = "Cattle",
            fromLocationId = "FARM-A",
            toLocationId = "FARM-B",
            movementDate = "2026-11-20",
            recordedBy = "Jane Doe",
            notes = "Initial transfer"
        });

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Advance clock by 1 minute for same-or-later movement
        _factory.Clock.Advance(TimeSpan.FromMinutes(1));

        var secondResponse = await authedClient.PostAsJsonAsync("/api/movements", new
        {
            animalId,
            species = "Cattle",
            fromLocationId = "FARM-B",
            toLocationId = "FARM-C",
            movementDate = "2026-11-21",
            recordedBy = "Jane Doe"
        });

        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        // Verify history
        var historyResponse = await authedClient.GetAsync($"/api/animals/{animalId}/movements");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var history = await historyResponse.Content.ReadFromJsonAsync<AnimalMovementHistoryDto>(JsonOptions);
        Assert.NotNull(history);
        Assert.Equal(2, history.TotalMovements);
        Assert.Equal(Species.Cattle, history.Species);
        Assert.Equal("FARM-A", history.Movements[0].FromLocationId);
        Assert.Equal("FARM-C", history.Movements[1].ToLocationId);

        // Verify Outbox messages persisted atomically
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnimalMovementsDbContext>();
        var outboxCount = await db.OutboxMessages.CountAsync(o => o.PayloadJson.Contains(animalId));
        Assert.Equal(2, outboxCount);
    }

    [Fact]
    public async Task PostMovement_BusinessRuleViolations_Return422UnprocessableEntity()
    {
        var authedClient = await CreateAuthenticatedClientAsync();
        const string animalId = "982000999888777";

        // Seed initial movement
        var seedRes = await authedClient.PostAsJsonAsync("/api/movements", new
        {
            animalId,
            species = "Deer",
            fromLocationId = "FARM-1",
            toLocationId = "FARM-2",
            movementDate = "2026-11-20",
            recordedBy = "Jane Doe"
        });
        Assert.Equal(HttpStatusCode.Created, seedRes.StatusCode);

        _factory.Clock.Advance(TimeSpan.FromMinutes(5));

        // Species change -> 422
        var speciesRes = await authedClient.PostAsJsonAsync("/api/movements", new
        {
            animalId,
            species = "Cattle",
            fromLocationId = "FARM-2",
            toLocationId = "FARM-3",
            movementDate = "2026-11-21",
            recordedBy = "Jane Doe"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, speciesRes.StatusCode);

        // Future NZ date -> 422
        var futureRes = await authedClient.PostAsJsonAsync("/api/movements", new
        {
            animalId,
            species = "Deer",
            fromLocationId = "FARM-2",
            toLocationId = "FARM-3",
            movementDate = "2026-11-22",
            recordedBy = "Jane Doe"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, futureRes.StatusCode);

        // Location continuity mismatch -> 422
        var locRes = await authedClient.PostAsJsonAsync("/api/movements", new
        {
            animalId,
            species = "Deer",
            fromLocationId = "FARM-99",
            toLocationId = "FARM-3",
            movementDate = "2026-11-21",
            recordedBy = "Jane Doe"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, locRes.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "demo@animalmovements.local",
            password = "DemoPass123!"
        });
        loginRes.EnsureSuccessStatusCode();

        var token = await loginRes.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}

public sealed class FakeNzClock : INzClock
{
    public DateTimeOffset UtcNow { get; private set; } = new(2026, 11, 20, 22, 0, 0, TimeSpan.Zero);
    public DateOnly NzToday => new(2026, 11, 21);

    public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
}

public sealed class MovementsApiFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"animalmovements-test-{Guid.NewGuid():N}.db");

    public FakeNzClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}",
                ["Outbox:DisableBackgroundProcessor"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<INzClock>();
            services.AddSingleton<INzClock>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch { }
        }
    }
}
