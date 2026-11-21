# Multi-stage Dockerfile compatible with both Docker and Podman
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY AnimalMovements.sln ./
COPY src/AnimalMovements.Domain/AnimalMovements.Domain.csproj src/AnimalMovements.Domain/
COPY src/AnimalMovements.Application/AnimalMovements.Application.csproj src/AnimalMovements.Application/
COPY src/AnimalMovements.Infrastructure/AnimalMovements.Infrastructure.csproj src/AnimalMovements.Infrastructure/
COPY src/AnimalMovements.Api/AnimalMovements.Api.csproj src/AnimalMovements.Api/
COPY tests/AnimalMovements.Domain.Tests/AnimalMovements.Domain.Tests.csproj tests/AnimalMovements.Domain.Tests/
COPY tests/AnimalMovements.Api.Tests/AnimalMovements.Api.Tests.csproj tests/AnimalMovements.Api.Tests/

RUN dotnet restore AnimalMovements.sln

# Copy everything and publish
COPY . .
RUN dotnet publish src/AnimalMovements.Api/AnimalMovements.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create writable data directory for SQLite mode and run as non-root
RUN mkdir -p /app/data && chown -R app:app /app
USER app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development
EXPOSE 8080

ENTRYPOINT ["dotnet", "AnimalMovements.Api.dll"]
