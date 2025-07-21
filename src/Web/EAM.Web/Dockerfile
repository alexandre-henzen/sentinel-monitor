# EAM Web Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create non-root user
RUN addgroup --gid 1001 --system eam && \
    adduser --uid 1001 --system --gid 1001 eam

FROM node:18-alpine AS node-build
WORKDIR /src

# Copy package.json and package-lock.json
COPY ["src/Web/EAM.Web/ClientApp/package*.json", "./"]

# Install npm dependencies
RUN npm ci --only=production

# Copy Angular source code
COPY ["src/Web/EAM.Web/ClientApp/", "./"]

# Build Angular app
RUN npm run build -- --configuration production

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/Web/EAM.Web/EAM.Web.csproj", "src/Web/EAM.Web/"]
COPY ["src/EAM.Shared/EAM.Shared.csproj", "src/EAM.Shared/"]
COPY ["src/Shared/EAM.Shared.Contracts/EAM.Shared.Contracts.csproj", "src/Shared/EAM.Shared.Contracts/"]
COPY ["src/Shared/EAM.Shared.Security/EAM.Shared.Security.csproj", "src/Shared/EAM.Shared.Security/"]
COPY ["src/Shared/EAM.Shared.Telemetry/EAM.Shared.Telemetry.csproj", "src/Shared/EAM.Shared.Telemetry/"]

RUN dotnet restore "src/Web/EAM.Web/EAM.Web.csproj"

# Copy source code
COPY . .

# Copy built Angular app from node-build stage
COPY --from=node-build /src/dist /src/src/Web/EAM.Web/ClientApp/dist

# Build the application
WORKDIR "/src/src/Web/EAM.Web"
RUN dotnet build "EAM.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EAM.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Switch to non-root user
USER eam

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "EAM.Web.dll"]