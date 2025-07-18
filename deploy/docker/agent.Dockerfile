# EAM Agent Dockerfile - Windows Container
# Multi-stage build for optimized Windows agent container

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/EAM.Agent/EAM.Agent.csproj", "src/EAM.Agent/"]
COPY ["src/EAM.Shared/EAM.Shared.csproj", "src/EAM.Shared/"]
COPY ["tools/EAM.PluginSDK/EAM.PluginSDK.csproj", "tools/EAM.PluginSDK/"]

# Restore dependencies
RUN dotnet restore "src/EAM.Agent/EAM.Agent.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/src/EAM.Agent"
RUN dotnet build "EAM.Agent.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "EAM.Agent.csproj" \
    -c Release \
    -o /app/publish \
    --runtime win-x64 \
    --self-contained true \
    --no-restore \
    /p:PublishTrimmed=true \
    /p:PublishSingleFile=false \
    /p:PublishReadyToRun=true

# Runtime stage
FROM mcr.microsoft.com/windows/servercore:ltsc2022
WORKDIR /app

# Create application user
RUN net user eamuser /add && \
    net localgroup "Users" eamuser /add

# Create necessary directories
RUN mkdir C:\ProgramData\EAM && \
    mkdir C:\ProgramData\EAM\plugins && \
    mkdir C:\ProgramData\EAM\data && \
    mkdir C:\ProgramData\EAM\logs

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables
ENV DOTNET_ENVIRONMENT=Production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV EAM_DATA_PATH=C:\ProgramData\EAM\data
ENV EAM_PLUGINS_PATH=C:\ProgramData\EAM\plugins
ENV EAM_LOGS_PATH=C:\ProgramData\EAM\logs

# Configure Windows service
RUN sc create "EAM Agent" binPath="C:\app\EAM.Agent.exe" start=auto DisplayName="EAM Agent Service"

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD powershell -Command "try { \
        $service = Get-Service -Name 'EAM Agent' -ErrorAction Stop; \
        if ($service.Status -eq 'Running') { \
            $process = Get-Process -Name 'EAM.Agent' -ErrorAction Stop; \
            if ($process.WorkingSet -gt 0) { exit 0 } else { exit 1 } \
        } else { exit 1 } \
    } catch { exit 1 }"

# Expose telemetry ports
EXPOSE 4317 4318

# Set working directory permissions
RUN icacls C:\ProgramData\EAM /grant eamuser:F /T
RUN icacls C:\app /grant eamuser:RX /T

# Run as service
USER eamuser
ENTRYPOINT ["EAM.Agent.exe"]