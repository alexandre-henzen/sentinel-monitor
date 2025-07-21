# EAM Agent Dockerfile
# Note: This is primarily for testing and development purposes
# The production Agent will be deployed as a Windows Service via MSI installer

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Create non-root user
RUN addgroup --gid 1001 --system eam && \
    adduser --uid 1001 --system --gid 1001 eam

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/Agent/EAM.Agent/EAM.Agent.csproj", "src/Agent/EAM.Agent/"]
COPY ["src/Agent/EAM.Agent.Core/EAM.Agent.Core.csproj", "src/Agent/EAM.Agent.Core/"]
COPY ["src/EAM.Shared/EAM.Shared.csproj", "src/EAM.Shared/"]
COPY ["src/Shared/EAM.Shared.Contracts/EAM.Shared.Contracts.csproj", "src/Shared/EAM.Shared.Contracts/"]
COPY ["src/Shared/EAM.Shared.Security/EAM.Shared.Security.csproj", "src/Shared/EAM.Shared.Security/"]
COPY ["src/Shared/EAM.Shared.Telemetry/EAM.Shared.Telemetry.csproj", "src/Shared/EAM.Shared.Telemetry/"]

RUN dotnet restore "src/Agent/EAM.Agent/EAM.Agent.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/src/Agent/EAM.Agent"
RUN dotnet build "EAM.Agent.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EAM.Agent.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create directories for data and logs
RUN mkdir -p /app/Data /app/logs && \
    chown -R eam:eam /app/Data /app/logs

# Switch to non-root user
USER eam

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Development
ENV DOTNET_ENVIRONMENT=Development

# Health check (basic process check)
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD pgrep -f "EAM.Agent" || exit 1

ENTRYPOINT ["dotnet", "EAM.Agent.dll"]