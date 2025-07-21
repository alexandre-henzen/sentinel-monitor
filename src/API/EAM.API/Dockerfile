# EAM API Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create non-root user
RUN addgroup --gid 1001 --system eam && \
    adduser --uid 1001 --system --gid 1001 eam

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/API/EAM.API/EAM.API.csproj", "src/API/EAM.API/"]
COPY ["src/API/EAM.API.Core/EAM.API.Core.csproj", "src/API/EAM.API.Core/"]
COPY ["src/EAM.Shared/EAM.Shared.csproj", "src/EAM.Shared/"]
COPY ["src/Shared/EAM.Shared.Contracts/EAM.Shared.Contracts.csproj", "src/Shared/EAM.Shared.Contracts/"]
COPY ["src/Shared/EAM.Shared.Security/EAM.Shared.Security.csproj", "src/Shared/EAM.Shared.Security/"]
COPY ["src/Shared/EAM.Shared.Telemetry/EAM.Shared.Telemetry.csproj", "src/Shared/EAM.Shared.Telemetry/"]
COPY ["src/Infrastructure/EAM.Infrastructure.Data/EAM.Infrastructure.Data.csproj", "src/Infrastructure/EAM.Infrastructure.Data/"]
COPY ["src/Infrastructure/EAM.Infrastructure.Messaging/EAM.Infrastructure.Messaging.csproj", "src/Infrastructure/EAM.Infrastructure.Messaging/"]
COPY ["src/Infrastructure/EAM.Infrastructure.Storage/EAM.Infrastructure.Storage.csproj", "src/Infrastructure/EAM.Infrastructure.Storage/"]

RUN dotnet restore "src/API/EAM.API/EAM.API.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/src/API/EAM.API"
RUN dotnet build "EAM.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EAM.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Switch to non-root user
USER eam

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "EAM.API.dll"]