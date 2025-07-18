# Employee Activity Monitor (EAM) v5.0 - Blueprint de Arquitetura

*Documento gerado em: 18 jul 2025*

## Visão Geral

Este documento detalha a arquitetura completa do Employee Activity Monitor (EAM) v5.0, incluindo estrutura de pastas, configurações, e decisões técnicas para implementação.

## Estrutura de Solution (.NET)

### EAM.sln - Solution Principal

```
EAM.sln
├── src/
│   ├── EAM.Agent/                    # Agente Windows (.NET 8)
│   ├── EAM.API/                      # API RESTful (ASP.NET Core 8)
│   ├── EAM.Web/                      # Frontend Angular 18
│   └── EAM.Shared/                   # Bibliotecas compartilhadas
├── tests/
│   ├── EAM.Agent.Tests/
│   ├── EAM.API.Tests/
│   └── EAM.Shared.Tests/
├── tools/
│   ├── EAM.PluginSDK/               # SDK para desenvolvimento de plugins
│   └── EAM.Installer/               # Projeto MSI Installer
├── infrastructure/
│   ├── docker/
│   ├── k8s/
│   └── scripts/
└── docs/
    ├── api/
    ├── plugins/
    └── deployment/
```

## Projetos .NET 8

### EAM.Agent - Agente Windows

**Tipo:** Windows Service (.NET 8)
**Target Framework:** net8.0-windows
**Packages principais:**
- Microsoft.Extensions.Hosting.WindowsServices
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.Logging.Serilog
- OpenTelemetry.Exporter.OpenTelemetryProtocol
- System.Management
- Microsoft.UI.Xaml (para UI Automation)

#### Estrutura de Pastas:

```
EAM.Agent/
├── Services/
│   ├── Trackers/
│   │   ├── WindowTracker.cs
│   │   ├── BrowserTracker.cs
│   │   ├── TeamsTracker.cs
│   │   ├── ProcessMonitor.cs
│   │   └── ITracker.cs
│   ├── ScreenshotCapturer.cs
│   ├── ScoringEngine.cs
│   ├── TelemetryExporter.cs
│   └── SyncService.cs
├── Data/
│   ├── Entities/
│   │   ├── ActivityEvent.cs
│   │   ├── Screenshot.cs
│   │   └── ProcessInfo.cs
│   ├── DbContext/
│   │   └── LocalDbContext.cs
│   └── Repositories/
│       └── EventRepository.cs
├── Models/
│   ├── DTOs/
│   │   ├── ActivityEventDto.cs
│   │   ├── ScreenshotDto.cs
│   │   └── SyncBatchDto.cs
│   └── Configuration/
│       ├── AgentConfig.cs
│       └── TrackerConfig.cs
├── Helpers/
│   ├── WindowHelper.cs
│   ├── ProcessHelper.cs
│   ├── UIAutomationHelper.cs
│   └── CompressionHelper.cs
├── Plugins/
│   ├── ITrackerPlugin.cs
│   ├── PluginLoader.cs
│   ├── PluginManager.cs
│   └── PluginAssemblyLoadContext.cs
├── Program.cs
├── AgentService.cs
├── appsettings.json
└── EAM.Agent.csproj
```

#### Configuração Principal (appsettings.json):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Extensions.Hosting": "Warning"
    }
  },
  "Agent": {
    "MachineId": null,
    "ApiBaseUrl": "https://api.eam.local",
    "SyncIntervalSeconds": 60,
    "OfflineRetentionDays": 7
  },
  "Trackers": {
    "WindowTracker": {
      "Enabled": true,
      "IntervalSeconds": 1
    },
    "BrowserTracker": {
      "Enabled": true,
      "IntervalSeconds": 2
    },
    "TeamsTracker": {
      "Enabled": true,
      "IntervalSeconds": 5
    },
    "ScreenshotCapturer": {
      "Enabled": true,
      "IntervalSeconds": 60,
      "Quality": 75,
      "MaxWidth": 1920,
      "MaxHeight": 1080
    }
  },
  "Scoring": {
    "DefaultProductivityScore": 50,
    "Categories": {
      "Productive": ["outlook", "word", "excel", "powerpoint", "teams"],
      "Neutral": ["explorer", "notepad"],
      "Unproductive": ["games", "social", "entertainment"]
    }
  },
  "Telemetry": {
    "ServiceName": "EAM.Agent",
    "ServiceVersion": "5.0.0",
    "OtlpEndpoint": "http://localhost:4317",
    "ExportIntervalSeconds": 30
  },
  "Database": {
    "ConnectionString": "Data Source=%LOCALAPPDATA%\\EAM\\agent.db"
  },
  "Plugins": {
    "Directory": "%PROGRAMDATA%\\EAM\\plugins",
    "LoadOnStartup": true,
    "IsolationLevel": "Full"
  }
}
```

### EAM.API - API RESTful

**Tipo:** Web API (.NET 8)
**Target Framework:** net8.0
**Packages principais:**
- Microsoft.AspNetCore.OpenApi
- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.Extensions.Caching.Redis
- Minio
- OpenTelemetry.Extensions.Hosting
- Microsoft.AspNetCore.Authentication.JwtBearer
- Serilog.AspNetCore

#### Estrutura de Pastas:

```
EAM.API/
├── Controllers/
│   ├── EventsController.cs
│   ├── ScreenshotsController.cs
│   ├── AuthController.cs
│   ├── UpdatesController.cs
│   └── HealthController.cs
├── Services/
│   ├── EventIngestionService.cs
│   ├── ScreenshotService.cs
│   ├── AuthService.cs
│   ├── UpdateService.cs
│   └── TelemetryService.cs
├── Data/
│   ├── Entities/
│   │   ├── ActivityLog.cs
│   │   ├── Agent.cs
│   │   ├── User.cs
│   │   └── DailyScore.cs
│   ├── DbContext/
│   │   └── EamDbContext.cs
│   ├── Repositories/
│   │   ├── IEventRepository.cs
│   │   ├── EventRepository.cs
│   │   └── IAgentRepository.cs
│   └── Migrations/
├── Models/
│   ├── DTOs/
│   │   ├── EventBatchDto.cs
│   │   ├── ScreenshotUploadDto.cs
│   │   └── AuthResponseDto.cs
│   ├── Requests/
│   │   ├── EventIngestionRequest.cs
│   │   └── AuthRequest.cs
│   └── Responses/
│       ├── UploadUrlResponse.cs
│       └── UpdateResponse.cs
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs
│   ├── RequestLoggingMiddleware.cs
│   └── RateLimitingMiddleware.cs
├── Configuration/
│   ├── DatabaseConfig.cs
│   ├── StorageConfig.cs
│   └── TelemetryConfig.cs
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── appsettings.Production.json
└── EAM.API.csproj
```

#### Configuração API (appsettings.json):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=eam;Username=eam_user;Password=eam_pass;Port=5432",
    "Redis": "localhost:6379"
  },
  "JWT": {
    "Secret": "your-super-secret-key-here-min-32-chars",
    "Issuer": "EAM.API",
    "Audience": "EAM.Agent",
    "ExpiryMinutes": 60
  },
  "MinIO": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "BucketName": "eam-screenshots",
    "UseSSL": false,
    "Region": "us-east-1"
  },
  "Ingestion": {
    "BatchSize": 1000,
    "MaxBatchSizeMB": 10,
    "TimeoutSeconds": 30,
    "RetryAttempts": 3
  },
  "RateLimiting": {
    "EventsPerMinute": 10000,
    "RequestsPerMinute": 1000
  },
  "Telemetry": {
    "ServiceName": "EAM.API",
    "ServiceVersion": "5.0.0",
    "OtlpEndpoint": "http://localhost:4317",
    "ExportIntervalSeconds": 30
  },
  "Features": {
    "EnableMetrics": true,
    "EnableTracing": true,
    "EnableHealthChecks": true
  }
}
```

### EAM.Web - Frontend Angular 18

**Tipo:** Angular SPA
**Framework:** Angular 18 LTS
**UI Library:** PrimeNG 18

#### Estrutura de Pastas:

```
EAM.Web/
├── src/
│   ├── app/
│   │   ├── core/
│   │   │   ├── services/
│   │   │   │   ├── api.service.ts
│   │   │   │   ├── auth.service.ts
│   │   │   │   ├── websocket.service.ts
│   │   │   │   └── telemetry.service.ts
│   │   │   ├── guards/
│   │   │   │   ├── auth.guard.ts
│   │   │   │   └── role.guard.ts
│   │   │   ├── interceptors/
│   │   │   │   ├── auth.interceptor.ts
│   │   │   │   ├── error.interceptor.ts
│   │   │   │   └── loading.interceptor.ts
│   │   │   ├── models/
│   │   │   │   ├── user.model.ts
│   │   │   │   ├── activity.model.ts
│   │   │   │   └── agent.model.ts
│   │   │   └── core.module.ts
│   │   ├── shared/
│   │   │   ├── components/
│   │   │   │   ├── loading/
│   │   │   │   ├── confirmation-dialog/
│   │   │   │   ├── data-table/
│   │   │   │   └── chart-widget/
│   │   │   ├── pipes/
│   │   │   │   ├── duration.pipe.ts
│   │   │   │   ├── file-size.pipe.ts
│   │   │   │   └── relative-time.pipe.ts
│   │   │   ├── directives/
│   │   │   │   └── permission.directive.ts
│   │   │   └── shared.module.ts
│   │   ├── features/
│   │   │   ├── dashboard/
│   │   │   │   ├── components/
│   │   │   │   │   ├── overview-cards/
│   │   │   │   │   ├── activity-chart/
│   │   │   │   │   ├── top-applications/
│   │   │   │   │   └── productivity-score/
│   │   │   │   ├── services/
│   │   │   │   │   └── dashboard.service.ts
│   │   │   │   ├── dashboard.component.ts
│   │   │   │   └── dashboard.module.ts
│   │   │   ├── timeline/
│   │   │   │   ├── components/
│   │   │   │   │   ├── timeline-view/
│   │   │   │   │   ├── activity-details/
│   │   │   │   │   ├── screenshot-viewer/
│   │   │   │   │   └── filters/
│   │   │   │   ├── services/
│   │   │   │   │   └── timeline.service.ts
│   │   │   │   ├── timeline.component.ts
│   │   │   │   └── timeline.module.ts
│   │   │   ├── agents/
│   │   │   │   ├── components/
│   │   │   │   │   ├── agent-list/
│   │   │   │   │   ├── agent-details/
│   │   │   │   │   └── agent-status/
│   │   │   │   ├── services/
│   │   │   │   │   └── agent.service.ts
│   │   │   │   ├── agents.component.ts
│   │   │   │   └── agents.module.ts
│   │   │   ├── reports/
│   │   │   │   ├── components/
│   │   │   │   │   ├── report-builder/
│   │   │   │   │   ├── report-viewer/
│   │   │   │   │   └── export-options/
│   │   │   │   ├── services/
│   │   │   │   │   └── report.service.ts
│   │   │   │   ├── reports.component.ts
│   │   │   │   └── reports.module.ts
│   │   │   └── admin/
│   │   │       ├── components/
│   │   │       │   ├── user-management/
│   │   │       │   ├── system-settings/
│   │   │       │   └── audit-logs/
│   │   │       ├── services/
│   │   │       │   └── admin.service.ts
│   │   │       ├── admin.component.ts
│   │   │       └── admin.module.ts
│   │   ├── layout/
│   │   │   ├── header/
│   │   │   │   └── header.component.ts
│   │   │   ├── sidebar/
│   │   │   │   └── sidebar.component.ts
│   │   │   ├── footer/
│   │   │   │   └── footer.component.ts
│   │   │   └── main-layout/
│   │   │       └── main-layout.component.ts
│   │   ├── app.component.ts
│   │   ├── app.config.ts
│   │   └── app.routes.ts
│   ├── assets/
│   │   ├── images/
│   │   ├── icons/
│   │   ├── styles/
│   │   └── i18n/
│   ├── environments/
│   │   ├── environment.ts
│   │   └── environment.prod.ts
│   ├── styles.scss
│   ├── main.ts
│   └── index.html
├── angular.json
├── package.json
├── tsconfig.json
├── tailwind.config.js
└── README.md
```

#### Configuração Angular (package.json):

```json
{
  "name": "eam-web",
  "version": "5.0.0",
  "scripts": {
    "ng": "ng",
    "start": "ng serve",
    "build": "ng build",
    "watch": "ng build --watch --configuration development",
    "test": "ng test",
    "e2e": "ng e2e",
    "lint": "ng lint"
  },
  "dependencies": {
    "@angular/animations": "^18.0.0",
    "@angular/common": "^18.0.0",
    "@angular/compiler": "^18.0.0",
    "@angular/core": "^18.0.0",
    "@angular/forms": "^18.0.0",
    "@angular/platform-browser": "^18.0.0",
    "@angular/platform-browser-dynamic": "^18.0.0",
    "@angular/router": "^18.0.0",
    "@angular/service-worker": "^18.0.0",
    "primeng": "^18.0.0",
    "primeicons": "^7.0.0",
    "primeflex": "^3.3.0",
    "rxjs": "~7.8.0",
    "tslib": "^2.3.0",
    "zone.js": "~0.14.0",
    "@swimlane/ngx-charts": "^20.4.0",
    "date-fns": "^2.30.0",
    "chart.js": "^4.4.0",
    "ng2-charts": "^5.0.0"
  },
  "devDependencies": {
    "@angular-devkit/build-angular": "^18.0.0",
    "@angular/cli": "^18.0.0",
    "@angular/compiler-cli": "^18.0.0",
    "@types/jasmine": "~5.1.0",
    "@types/node": "^18.7.0",
    "jasmine-core": "~5.1.0",
    "karma": "~6.4.0",
    "karma-chrome-headless": "~3.1.0",
    "karma-coverage": "~2.2.0",
    "karma-jasmine": "~5.1.0",
    "karma-jasmine-html-reporter": "~2.1.0",
    "typescript": "~5.4.0",
    "tailwindcss": "^3.4.0",
    "autoprefixer": "^10.4.0",
    "postcss": "^8.4.0"
  }
}
```

### EAM.Shared - Bibliotecas Compartilhadas

**Tipo:** Class Library (.NET 8)
**Target Framework:** net8.0

#### Estrutura de Pastas:

```
EAM.Shared/
├── Models/
│   ├── Common/
│   │   ├── ActivityEvent.cs
│   │   ├── Screenshot.cs
│   │   ├── Agent.cs
│   │   └── User.cs
│   ├── DTOs/
│   │   ├── EventDto.cs
│   │   ├── ScreenshotDto.cs
│   │   ├── AgentDto.cs
│   │   └── UserDto.cs
│   └── Enums/
│       ├── ActivityType.cs
│       ├── AgentStatus.cs
│       └── EventCategory.cs
├── Constants/
│   ├── ApiEndpoints.cs
│   ├── ConfigKeys.cs
│   └── MimeTypes.cs
├── Extensions/
│   ├── DateTimeExtensions.cs
│   ├── StringExtensions.cs
│   └── CollectionExtensions.cs
├── Utilities/
│   ├── HashHelper.cs
│   ├── CompressionHelper.cs
│   └── ValidationHelper.cs
├── Interfaces/
│   ├── IEventProcessor.cs
│   ├── IScreenshotHandler.cs
│   └── ITelemetryProvider.cs
└── EAM.Shared.csproj
```

## Infraestrutura

### Docker Compose - Desenvolvimento

```yaml
version: '3.8'
services:
  postgresql:
    image: postgres:16
    environment:
      POSTGRES_DB: eam
      POSTGRES_USER: eam_user
      POSTGRES_PASSWORD: eam_pass
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./infrastructure/sql/init.sql:/docker-entrypoint-initdb.d/init.sql
    networks:
      - eam-network

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    networks:
      - eam-network

  minio:
    image: minio/minio:latest
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: minioadmin
    ports:
      - "9000:9000"
      - "9001:9001"
    volumes:
      - minio_data:/data
    networks:
      - eam-network

  grafana:
    image: grafana/grafana:10.0.0
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana_data:/var/lib/grafana
      - ./infrastructure/grafana/dashboards:/etc/grafana/provisioning/dashboards
      - ./infrastructure/grafana/datasources:/etc/grafana/provisioning/datasources
    networks:
      - eam-network

  loki:
    image: grafana/loki:3.0.0
    ports:
      - "3100:3100"
    command: -config.file=/etc/loki/local-config.yaml
    volumes:
      - ./infrastructure/loki/loki-config.yaml:/etc/loki/local-config.yaml
      - loki_data:/loki
    networks:
      - eam-network

  tempo:
    image: grafana/tempo:2.2.0
    command: [ "-config.file=/etc/tempo.yaml" ]
    volumes:
      - ./infrastructure/tempo/tempo.yaml:/etc/tempo.yaml
      - tempo_data:/tmp/tempo
    ports:
      - "3200:3200"
      - "4317:4317"  # OTLP gRPC
      - "4318:4318"  # OTLP HTTP
    networks:
      - eam-network

  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    command: ["--config=/etc/otel-collector-config.yaml"]
    volumes:
      - ./infrastructure/otel/otel-collector-config.yaml:/etc/otel-collector-config.yaml
    ports:
      - "8888:8888"   # Prometheus metrics
      - "8889:8889"   # Prometheus exporter metrics
      - "13133:13133" # health_check extension
      - "4317:4317"   # OTLP gRPC receiver
      - "4318:4318"   # OTLP HTTP receiver
    depends_on:
      - loki
      - tempo
    networks:
      - eam-network

volumes:
  postgres_data:
  redis_data:
  minio_data:
  grafana_data:
  loki_data:
  tempo_data:

networks:
  eam-network:
    driver: bridge
```

### Scripts de Configuração

#### Setup de Banco de Dados (init.sql):

```sql
-- Criação do banco de dados e esquemas
CREATE SCHEMA IF NOT EXISTS eam;

-- Extensões necessárias
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";

-- Tabela de agentes
CREATE TABLE eam.agents (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    machine_id VARCHAR(255) NOT NULL UNIQUE,
    machine_name VARCHAR(255) NOT NULL,
    user_name VARCHAR(255) NOT NULL,
    os_version VARCHAR(255),
    agent_version VARCHAR(50),
    last_seen TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    status VARCHAR(20) DEFAULT 'Active',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Tabela de logs de atividade (particionada por data)
CREATE TABLE eam.activity_logs (
    id UUID DEFAULT uuid_generate_v4(),
    agent_id UUID NOT NULL REFERENCES eam.agents(id),
    event_type VARCHAR(50) NOT NULL,
    application_name VARCHAR(255),
    window_title VARCHAR(500),
    url VARCHAR(2000),
    process_name VARCHAR(255),
    process_id INTEGER,
    duration_seconds INTEGER,
    productivity_score INTEGER,
    screenshot_path VARCHAR(500),
    metadata JSONB,
    event_timestamp TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    PRIMARY KEY (id, event_timestamp)
) PARTITION BY RANGE (event_timestamp);

-- Partições para os próximos 12 meses
DO $$
DECLARE
    start_date DATE := DATE_TRUNC('month', CURRENT_DATE);
    end_date DATE;
BEGIN
    FOR i IN 0..11 LOOP
        end_date := start_date + INTERVAL '1 month';
        EXECUTE format('CREATE TABLE eam.activity_logs_%s PARTITION OF eam.activity_logs 
                       FOR VALUES FROM (%L) TO (%L)',
                       to_char(start_date, 'YYYY_MM'),
                       start_date,
                       end_date);
        start_date := end_date;
    END LOOP;
END $$;

-- Índices para performance
CREATE INDEX idx_activity_logs_agent_timestamp ON eam.activity_logs (agent_id, event_timestamp);
CREATE INDEX idx_activity_logs_event_type ON eam.activity_logs (event_type);
CREATE INDEX idx_activity_logs_application ON eam.activity_logs (application_name);
CREATE INDEX idx_activity_logs_metadata ON eam.activity_logs USING GIN (metadata);

-- View materializada para scores diários
CREATE MATERIALIZED VIEW eam.daily_scores AS
SELECT 
    agent_id,
    DATE(event_timestamp) as activity_date,
    AVG(productivity_score) as avg_productivity,
    SUM(duration_seconds) as total_active_seconds,
    COUNT(*) as total_events,
    COUNT(DISTINCT application_name) as unique_applications
FROM eam.activity_logs
GROUP BY agent_id, DATE(event_timestamp);

-- Índice para a view materializada
CREATE UNIQUE INDEX idx_daily_scores_agent_date ON eam.daily_scores (agent_id, activity_date);

-- Usuários e permissões
CREATE TABLE eam.users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    username VARCHAR(255) NOT NULL UNIQUE,
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(50) DEFAULT 'User',
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Inserir usuário admin padrão
INSERT INTO eam.users (username, email, password_hash, role) 
VALUES ('admin', 'admin@eam.local', '$2a$11$example.hash.here', 'Admin');

-- Função para atualizar o timestamp updated_at
CREATE OR REPLACE FUNCTION eam.update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Triggers para atualizar updated_at
CREATE TRIGGER update_agents_updated_at BEFORE UPDATE ON eam.agents 
    FOR EACH ROW EXECUTE FUNCTION eam.update_updated_at_column();
    
CREATE TRIGGER update_users_updated_at BEFORE UPDATE ON eam.users 
    FOR EACH ROW EXECUTE FUNCTION eam.update_updated_at_column();
```

## Sistema de Plugins

### Interface do Plugin SDK

```csharp
// EAM.PluginSDK/ITrackerPlugin.cs
namespace EAM.PluginSDK
{
    public interface ITrackerPlugin
    {
        string Name { get; }
        string Version { get; }
        string Description { get; }
        
        ValueTask InitializeAsync(IConfiguration configuration, ILogger logger);
        ValueTask<IEnumerable<ActivityEvent>> PollAsync(CancellationToken cancellationToken);
        ValueTask<bool> IsEnabledAsync();
        ValueTask StopAsync();
    }
    
    public class ActivityEvent
    {
        public string EventType { get; set; }
        public string ApplicationName { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        public int ProcessId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
        public int ProductivityScore { get; set; }
    }
}
```

### Estrutura de Plugins

```
%PROGRAMDATA%\EAM\plugins\
├── SlackTracker\
│   ├── SlackTracker.dll
│   ├── SlackTracker.pdb
│   ├── plugin.json
│   └── config.json
├── OutlookTracker\
│   ├── OutlookTracker.dll
│   ├── OutlookTracker.pdb
│   ├── plugin.json
│   └── config.json
└── CustomApp\
    ├── CustomApp.dll
    ├── CustomApp.pdb
    ├── plugin.json
    └── config.json
```

### Configuração do Plugin (plugin.json):

```json
{
  "name": "SlackTracker",
  "version": "1.0.0",
  "description": "Tracks Slack activity and meetings",
  "author": "EAM Team",
  "assembly": "SlackTracker.dll",
  "entryPoint": "SlackTracker.SlackTrackerPlugin",
  "dependencies": [
    "System.Text.Json"
  ],
  "permissions": [
    "ProcessAccess",
    "WindowAccess",
    "NetworkAccess"
  ],
  "configuration": {
    "enabled": true,
    "pollIntervalSeconds": 5,
    "trackChannels": true,
    "trackDirectMessages": true,
    "productivityScores": {
      "channels": 70,
      "directMessages": 80,
      "calls": 90
    }
  }
}
```

## Telemetria e Observabilidade

### Configuração OpenTelemetry

#### OTEL Collector (otel-collector-config.yaml):

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

processors:
  batch:
    timeout: 1s
    send_batch_size: 1024
    send_batch_max_size: 2048
  
  resource:
    attributes:
      - key: service.environment
        value: "development"
        action: upsert

exporters:
  loki:
    endpoint: http://loki:3100/loki/api/v1/push
    
  tempo:
    endpoint: tempo:4317
    tls:
      insecure: true
      
  prometheus:
    endpoint: "0.0.0.0:8889"

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [batch, resource]
      exporters: [tempo]
      
    metrics:
      receivers: [otlp]
      processors: [batch, resource]
      exporters: [prometheus]
      
    logs:
      receivers: [otlp]
      processors: [batch, resource]
      exporters: [loki]
```

### Configuração Loki (loki-config.yaml):

```yaml
auth_enabled: false

server:
  http_listen_port: 3100

common:
  path_prefix: /loki
  storage:
    filesystem:
      chunks_directory: /loki/chunks
      rules_directory: /loki/rules
  replication_factor: 1
  ring:
    instance_addr: 127.0.0.1
    kvstore:
      store: inmemory

query_scheduler:
  max_outstanding_requests_per_tenant: 2048

frontend:
  max_outstanding_per_tenant: 2048

schema_config:
  configs:
    - from: 2020-10-24
      store: boltdb-shipper
      object_store: filesystem
      schema: v11
      index:
        prefix: index_
        period: 24h

storage_config:
  boltdb_shipper:
    active_index_directory: /loki/boltdb-shipper-active
    cache_location: /loki/boltdb-shipper-cache
    shared_store: filesystem
  filesystem:
    directory: /loki/chunks

limits_config:
  enforce_metric_name: false
  reject_old_samples: true
  reject_old_samples_max_age: 168h
  ingestion_rate_mb: 16
  ingestion_burst_size_mb: 32

ruler:
  storage:
    type: local
    local:
      directory: /loki/rules
  rule_path: /loki/rules
  alertmanager_url: http://localhost:9093
  ring:
    kvstore:
      store: inmemory
  enable_api: true
```

### Configuração Tempo (tempo.yaml):

```yaml
server:
  http_listen_port: 3200

distributor:
  receivers:
    otlp:
      protocols:
        grpc:
          endpoint: 0.0.0.0:4317
        http:
          endpoint: 0.0.0.0:4318

ingester:
  max_block_duration: 5m

compactor:
  compaction:
    compaction_window: 1h
    max_compaction_objects: 1000000
    block_retention: 1h
    compacted_block_retention: 10m

storage:
  trace:
    backend: local
    local:
      path: /tmp/tempo/traces
    wal:
      path: /tmp/tempo/wal
    pool:
      max_workers: 100
      queue_depth: 10000
```

## Decisões Arquiteturais

### 1. Estrutura de Solution

**Decisão:** Usar uma única solution (`EAM.sln`) com múltiplos projetos organizados por tipo.

**Justificativa:**
- Facilita o desenvolvimento e debugging
- Permite compartilhamento de código através do `EAM.Shared`
- Simplifica o processo de build e CI/CD
- Mantém a coesão da solução

### 2. Separação de Responsabilidades

**Decisão:** Cada componente tem responsabilidades bem definidas:
- `EAM.Agent`: Captura de dados e sincronização
- `EAM.API`: Ingestão, autenticação e persistência
- `EAM.Web`: Interface de usuário e visualização
- `EAM.Shared`: Modelos e utilitários compartilhados

**Justificativa:**
- Facilita manutenção e evolução independente
- Permite deployment separado de componentes
- Reduz acoplamento entre camadas

### 3. Sistema de Plugins

**Decisão:** Usar `AssemblyLoadContext` para isolamento de plugins.

**Justificativa:**
- Permite carregamento dinâmico de funcionalidades
- Isola plugins para evitar conflitos
- Facilita atualizações sem reiniciar o agente
- Suporta hot-reload de plugins

### 4. Persistência

**Decisão:** 
- PostgreSQL para dados estruturados
- MinIO para screenshots
- Redis para cache e sessões
- SQLite local no agente

**Justificativa:**
- PostgreSQL oferece particionamento e performance para dados temporais
- MinIO fornece armazenamento de objetos compatível com S3
- Redis proporciona cache rápido e gerenciamento de sessões
- SQLite local garante funcionamento offline

### 5. Telemetria

**Decisão:** Usar OpenTelemetry com OTLP e Grafana Stack.

**Justificativa:**
- Padrão da indústria para observabilidade
- Compatível com múltiplos backends
- Suporte nativo no .NET 8
- Integração com Grafana para visualização

### 6. Autenticação

**Decisão:** JWT para agentes, OIDC para usuários web.

**Justificativa:**
- JWT é stateless e adequado para agentes
- OIDC fornece melhor experiência para usuários web
- Permite revogação via Redis
- Suporta multiple providers

### 7. Arquitetura Frontend

**Decisão:** Angular 18 com standalone components e PrimeNG.

**Justificativa:**
- Angular 18 LTS oferece suporte de longo prazo
- Standalone components reduzem complexidade
- PrimeNG fornece componentes ricos e consistentes
- Suporta SSR para melhor performance

## Próximos Passos

1. **Implementação da Estrutura Base**
   - Criar projects e solution files
   - Configurar dependencies e packages
   - Implementar estrutura de pastas

2. **Desenvolvimento dos Trackers**
   - Implementar WindowTracker, BrowserTracker, etc.
   - Criar sistema de plugins
   - Implementar captura de screenshots

3. **Desenvolvimento da API**
   - Implementar endpoints de ingestão
   - Configurar autenticação JWT
   - Implementar integração com MinIO

4. **Desenvolvimento do Frontend**
   - Criar componentes base
   - Implementar dashboard e timeline
   - Configurar roteamento e guards

5. **Integração e Testes**
   - Testes unitários e integração
   - Configurar CI/CD
   - Deploy e monitoramento

Este blueprint fornece a base completa para implementação do EAM v5.0, seguindo exatamente as especificações técnicas definidas.