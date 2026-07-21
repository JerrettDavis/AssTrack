# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY AssTrack.sln .
COPY src/AssTrack.Api/AssTrack.Api.csproj src/AssTrack.Api/
COPY src/AssTrack.Domain/AssTrack.Domain.csproj src/AssTrack.Domain/
COPY src/AssTrack.Infrastructure/AssTrack.Infrastructure.csproj src/AssTrack.Infrastructure/

# Restore dependencies (restore only the API project to avoid needing test project files)
RUN dotnet restore src/AssTrack.Api/AssTrack.Api.csproj

# Copy all source files
COPY src/ src/

# Publish the API
RUN dotnet publish src/AssTrack.Api/AssTrack.Api.csproj -c Release --no-restore -o /app/publish /p:BuildIntegratedFrontend=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0.10
WORKDIR /app

# Install curl for the Docker healthcheck in docker-compose.yml.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Create data directory for SQLite
RUN mkdir -p /data

# Copy published app
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["dotnet", "AssTrack.Api.dll"]
