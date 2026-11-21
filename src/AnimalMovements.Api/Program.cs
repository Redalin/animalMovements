using System.Text;
using System.Text.Json.Serialization;
using AnimalMovements.Api.Endpoints;
using AnimalMovements.Api.Middleware;
using AnimalMovements.Api.Services;
using AnimalMovements.Application;
using AnimalMovements.Application.Abstractions;
using AnimalMovements.Infrastructure;
using AnimalMovements.Infrastructure.Auth;
using AnimalMovements.Infrastructure.Logging;
using AnimalMovements.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;

var logStore = new InMemoryLogStore();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .MinimumLevel.Override("AnimalMovements", LogEventLevel.Verbose)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
        .WriteTo.Sink(new InMemoryLogSink(logStore));
});

builder.Services.AddSingleton(logStore);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();
builder.Services.AddSingleton<BusinessRuleTestRunner>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (authOptions.IsLocal)
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = authOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = authOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.JwtSigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        }
        else
        {
            options.Authority = authOptions.Authority;
            options.Audience = authOptions.Audience;
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        }
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Animal Movements API",
        Version = "v1",
        Description = "DDD / Onion Architecture REST API for recording and retrieving animal movements with Outbox eventual consistency and pluggable identity."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Obtain a token from POST /api/auth/login (or your external IdP) and paste it here."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AnimalMovementsDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    var localIdentity = scope.ServiceProvider.GetRequiredService<ILocalIdentityService>();
    await localIdentity.EnsureDemoUserSeededAsync();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Animal Movements API v1");
    options.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestampUtc = DateTimeOffset.UtcNow }))
    .AllowAnonymous()
    .WithTags("Health");

app.MapAuthEndpoints(authOptions);
app.MapMovementEndpoints();
app.MapDiagnosticsEndpoints();

app.Run();

public partial class Program;
