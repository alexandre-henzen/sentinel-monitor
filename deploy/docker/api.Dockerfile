# EAM API Dockerfile - Linux Container
# Multi-stage build for optimized API container

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Install build dependencies
RUN apk add --no-cache git

# Copy csproj files and restore dependencies
COPY ["src/EAM.API/EAM.API.csproj", "src/EAM.API/"]
COPY ["src/EAM.Shared/EAM.Shared.csproj", "src/EAM.Shared/"]
COPY ["tools/EAM.PluginSDK/EAM.PluginSDK.csproj", "tools/EAM.PluginSDK/"]

# Restore dependencies
RUN dotnet restore "src/EAM.API/EAM.API.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/src/EAM.API"
RUN dotnet build "EAM.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "EAM.API.csproj" \
    -c Release \
    -o /app/publish \
    --runtime linux-x64 \
    --self-contained false \
    --no-restore \
    /p:PublishTrimmed=false \
    /p:PublishReadyToRun=true

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app

# Install runtime dependencies
RUN apk add --no-cache \
    curl \
    tzdata \
    && rm -rf /var/cache/apk/*

# Create non-root user
RUN addgroup -g 1000 eam && \
    adduser -u 1000 -G eam -s /bin/sh -D eam

# Create necessary directories
RUN mkdir -p /app/data /app/logs /app/temp && \
    chown -R eam:eam /app

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables
ENV DOTNET_ENVIRONMENT=Production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Configure timezone
ENV TZ=UTC

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Expose port
EXPOSE 8080

# Switch to non-root user
USER eam

# Set entrypoint
ENTRYPOINT ["dotnet", "EAM.API.dll"]