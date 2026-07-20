# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src

COPY AssTrack.sln .
COPY src/AssTrack.BridgeGateway/AssTrack.BridgeGateway.csproj src/AssTrack.BridgeGateway/
COPY src/AssTrack.Domain/AssTrack.Domain.csproj src/AssTrack.Domain/

RUN dotnet restore src/AssTrack.BridgeGateway/AssTrack.BridgeGateway.csproj

COPY src/ src/

RUN dotnet publish src/AssTrack.BridgeGateway/AssTrack.BridgeGateway.csproj -c Release --no-restore -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0.10
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["dotnet", "AssTrack.BridgeGateway.dll"]
