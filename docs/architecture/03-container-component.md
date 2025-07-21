# Employee Activity Monitor (EAM) v5.0 - Container & Component (C4 Model)

## 1. Container Diagram

### Visão Geral dos Containers
O sistema EAM é composto por múltiplos containers que trabalham em conjunto para fornecer funcionalidade completa de monitoramento.

```mermaid
graph TB
    subgraph "Usuários"
        Funcionario[Funcionário]
        Gerente[Gerente/Supervisor]
        Admin[Administrador TI]
    end
    
    subgraph "EAM System"
        Agent[EAM Agent<br/>Aplicação Windows<br/>.NET 8]
        WebApp[Web Application<br/>SPA Angular 18<br/>TypeScript]
        API[REST API<br/>ASP.NET Core 8<br/>C#]
        
        subgraph "Armazenamento"
            PostgreSQL[PostgreSQL 16<br/>Banco Principal]
            Redis[Redis 7<br/>Cache & Sessions]
            MinIO[MinIO<br/>Screenshots & Files]
        end
        
        subgraph "Observabilidade"
            Telemetry[OpenTelemetry<br/>Métricas & Traces]
            Grafana[Grafana<br/>Dashboards]
            Loki[Loki<br/>Logs]
            Tempo[Tempo<br/>Traces]
        end
    end
    
    subgraph "Sistemas Externos"
        AD[Active Directory]
        Teams[Microsoft Teams]
        Email[Email Server]
    end
    
    %% Relacionamentos Usuários
    Funcionario -.->|Monitora atividades| Agent
    Gerente -->|Visualiza relatórios| WebApp
    Admin -->|Configura sistema| WebApp
    
    %% Relacionamentos Containers
    Agent <-->|HTTPS/JSON| API
    WebApp <-->|HTTPS/JSON| API
    API <-->|SQL| PostgreSQL
    API <-->|Cache| Redis
    API <-->|Object Storage| MinIO
    
    %% Sistemas Externos
    API <-->|LDAP/LDAPS| AD
    API <-->|Graph API| Teams
    API -->|SMTP| Email
    
    %% Observabilidade
    Agent -->|Telemetry| Telemetry
    API -->|Telemetry| Telemetry
    WebApp -->|Telemetry| Telemetry
    Telemetry -->|Metrics| Grafana
    Telemetry -->|Logs| Loki
    Telemetry -->|Traces| Tempo
    
    style Agent fill:#e8f5e8
    style WebApp fill:#e3f2fd
    style API fill:#fff3e0
    style PostgreSQL fill:#f3e5f5
    style Redis fill:#ffebee
    style MinIO fill:#e0f2f1
```

## 2. Container Specifications

### 2.1 EAM Agent (Container)
- **Tipo**: Aplicação Windows Desktop
- **Tecnologia**: .NET 8, WPF (interface minimal)
- **Função**: Coleta dados da estação de trabalho
- **Deployment**: MSI Installer via Group Policy

#### Responsabilidades:
- Captura foco de janelas ativas
- Monitora URLs de navegadores
- Detecta reuniões Microsoft Teams
- Captura screenshots periódicos
- Envia dados via HTTPS para API

#### Configuração:
```json
{
  "ApiEndpoint": "https://eam-api.company.com",
  "ScreenshotInterval": 300,
  "DataSyncInterval": 60,
  "EnableWebTracking": true,
  "EnableTeamsTracking": true
}
```

### 2.2 Web Application (Container)
- **Tipo**: Single Page Application
- **Tecnologia**: Angular 18, TypeScript
- **Função**: Interface web para usuários
- **Deployment**: Nginx container

#### Responsabilidades:
- Dashboard executivo
- Relatórios de produtividade
- Configurações do sistema
- Gestão de usuários
- Visualização de dados

#### Tecnologias:
- Angular 18 + Angular Material
- PrimeNG para componentes avançados
- Chart.js para gráficos
- NgRx para gerenciamento de estado
- PWA para acesso offline

### 2.3 REST API (Container)
- **Tipo**: Web API
- **Tecnologia**: ASP.NET Core 8, C#
- **Função**: Backend principal do sistema
- **Deployment**: Docker container

#### Responsabilidades:
- Recebe dados do agente
- Autentica usuários
- Processa e armazena dados
- Fornece endpoints para web app
- Integra com sistemas externos

#### Arquitetura:
- Clean Architecture
- CQRS pattern
- Mediator pattern
- Repository pattern
- Unit of Work pattern

### 2.4 PostgreSQL (Container)
- **Tipo**: Banco de dados relacional
- **Tecnologia**: PostgreSQL 16
- **Função**: Armazenamento principal
- **Deployment**: Docker container

#### Responsabilidades:
- Dados de usuários
- Logs de atividades
- Configurações do sistema
- Relatórios e métricas
- Audit trails

### 2.5 Redis (Container)
- **Tipo**: Cache em memória
- **Tecnologia**: Redis 7
- **Função**: Cache e sessões
- **Deployment**: Docker container

#### Responsabilidades:
- Cache de consultas frequentes
- Sessões de usuários
- Rate limiting
- Pub/Sub para notificações
- Distributed locks

### 2.6 MinIO (Container)
- **Tipo**: Object Storage
- **Tecnologia**: MinIO S3-compatible
- **Função**: Armazenamento de arquivos
- **Deployment**: Docker container

#### Responsabilidades:
- Screenshots dos usuários
- Arquivos de configuração
- Backups automáticos
- Logs de auditoria
- Relatórios exportados

## 3. Component Diagram - EAM Agent

```mermaid
graph TB
    subgraph "EAM Agent Process"
        Main[Main Service<br/>Windows Service]
        
        subgraph "Data Collectors"
            WindowCollector[Window Focus<br/>Collector]
            URLCollector[URL Tracker<br/>Collector]
            TeamsCollector[Teams Meeting<br/>Collector]
            ScreenCollector[Screenshot<br/>Collector]
        end
        
        subgraph "Core Services"
            DataProcessor[Data Processor<br/>Service]
            ConfigService[Configuration<br/>Service]
            SecurityService[Security<br/>Service]
            CommsService[Communication<br/>Service]
        end
        
        subgraph "Storage"
            LocalDB[Local SQLite<br/>Buffer]
            FileSystem[Local Files<br/>Screenshots]
        end
    end
    
    subgraph "Windows APIs"
        WindowsAPI[Windows API<br/>User32, Kernel32]
        BrowserAPI[Browser APIs<br/>Chrome, Firefox, Edge]
        TeamsAPI[Teams Client<br/>Log Files]
    end
    
    subgraph "External"
        EAMAPI[EAM API<br/>REST Endpoints]
    end
    
    %% Fluxo de dados
    Main --> WindowCollector
    Main --> URLCollector
    Main --> TeamsCollector
    Main --> ScreenCollector
    
    WindowCollector --> DataProcessor
    URLCollector --> DataProcessor
    TeamsCollector --> DataProcessor
    ScreenCollector --> DataProcessor
    
    DataProcessor --> LocalDB
    DataProcessor --> FileSystem
    DataProcessor --> CommsService
    
    ConfigService --> Main
    SecurityService --> CommsService
    CommsService --> EAMAPI
    
    %% APIs externas
    WindowCollector <--> WindowsAPI
    URLCollector <--> BrowserAPI
    TeamsCollector <--> TeamsAPI
    ScreenCollector <--> WindowsAPI
    
    style Main fill:#e8f5e8
    style DataProcessor fill:#fff3e0
    style CommsService fill:#e3f2fd
```

## 4. Component Diagram - REST API

```mermaid
graph TB
    subgraph "EAM API"
        subgraph "Controllers"
            AuthController[Auth Controller<br/>JWT Authentication]
            DataController[Data Controller<br/>Agent Data Ingestion]
            ReportController[Report Controller<br/>Analytics & Reports]
            UserController[User Controller<br/>User Management]
            ConfigController[Config Controller<br/>System Configuration]
        end
        
        subgraph "Application Services"
            AuthService[Authentication<br/>Service]
            DataService[Data Processing<br/>Service]
            ReportService[Report Generation<br/>Service]
            UserService[User Management<br/>Service]
            NotificationService[Notification<br/>Service]
        end
        
        subgraph "Domain Services"
            ActivityDomain[Activity<br/>Domain Service]
            UserDomain[User<br/>Domain Service]
            SecurityDomain[Security<br/>Domain Service]
            IntegrationDomain[Integration<br/>Domain Service]
        end
        
        subgraph "Infrastructure"
            DataRepository[Data<br/>Repository]
            UserRepository[User<br/>Repository]
            ConfigRepository[Config<br/>Repository]
            CacheService[Cache<br/>Service]
            StorageService[Storage<br/>Service]
            EmailService[Email<br/>Service]
        end
        
        subgraph "External Integrations"
            ADService[Active Directory<br/>Integration]
            TeamsService[Teams Graph API<br/>Integration]
            TelemetryService[OpenTelemetry<br/>Service]
        end
    end
    
    subgraph "External Systems"
        PostgreSQL[(PostgreSQL<br/>Database)]
        Redis[(Redis<br/>Cache)]
        MinIO[(MinIO<br/>Storage)]
        ActiveDirectory[Active Directory]
        MicrosoftGraph[Microsoft Graph]
        SMTPServer[SMTP Server]
    end
    
    %% Controller -> Service
    AuthController --> AuthService
    DataController --> DataService
    ReportController --> ReportService
    UserController --> UserService
    ConfigController --> UserService
    
    %% Service -> Domain
    AuthService --> SecurityDomain
    DataService --> ActivityDomain
    ReportService --> ActivityDomain
    UserService --> UserDomain
    
    %% Service -> Repository
    DataService --> DataRepository
    UserService --> UserRepository
    ReportService --> DataRepository
    AuthService --> UserRepository
    
    %% Infrastructure
    DataRepository --> PostgreSQL
    UserRepository --> PostgreSQL
    ConfigRepository --> PostgreSQL
    CacheService --> Redis
    StorageService --> MinIO
    EmailService --> SMTPServer
    
    %% External Services
    ADService --> ActiveDirectory
    TeamsService --> MicrosoftGraph
    AuthService --> ADService
    DataService --> TeamsService
    NotificationService --> EmailService
    
    style AuthController fill:#ffebee
    style DataController fill:#e8f5e8
    style ReportController fill:#e3f2fd
    style UserController fill:#fff3e0
```

## 5. Component Diagram - Web Application

```mermaid
graph TB
    subgraph "Angular Web Application"
        subgraph "Core Module"
            AppComponent[App Component<br/>Root Component]
            AuthGuard[Auth Guard<br/>Route Protection]
            AuthService[Auth Service<br/>Authentication]
            HttpInterceptor[HTTP Interceptor<br/>Token & Error Handling]
        end
        
        subgraph "Shared Module"
            SharedComponents[Shared Components<br/>Reusable UI]
            SharedServices[Shared Services<br/>Common Logic]
            SharedPipes[Shared Pipes<br/>Data Transformation]
        end
        
        subgraph "Feature Modules"
            DashboardModule[Dashboard Module<br/>Main Overview]
            ReportsModule[Reports Module<br/>Analytics & Reports]
            UsersModule[Users Module<br/>User Management]
            SettingsModule[Settings Module<br/>Configuration]
        end
        
        subgraph "Services"
            DataService[Data Service<br/>API Communication]
            StateService[State Service<br/>NgRx Store]
            NotificationService[Notification Service<br/>User Feedback]
            ChartService[Chart Service<br/>Data Visualization]
        end
        
        subgraph "State Management"
            AppStore[NgRx Store<br/>Application State]
            Effects[NgRx Effects<br/>Side Effects]
            Selectors[NgRx Selectors<br/>State Queries]
        end
    end
    
    subgraph "External"
        EAMAPI[EAM REST API<br/>Backend Services]
        Browser[Browser APIs<br/>Local Storage, etc.]
    end
    
    %% Component relationships
    AppComponent --> AuthGuard
    AppComponent --> DashboardModule
    AppComponent --> ReportsModule
    AppComponent --> UsersModule
    AppComponent --> SettingsModule
    
    %% Services
    AuthService --> DataService
    DataService --> HttpInterceptor
    StateService --> AppStore
    AppStore --> Effects
    AppStore --> Selectors
    
    %% External communication
    DataService --> EAMAPI
    AuthService --> Browser
    HttpInterceptor --> EAMAPI
    
    %% Shared resources
    DashboardModule --> SharedComponents
    ReportsModule --> SharedComponents
    UsersModule --> SharedComponents
    SettingsModule --> SharedComponents
    
    style AppComponent fill:#e3f2fd
    style DataService fill:#e8f5e8
    style AppStore fill:#fff3e0
    style EAMAPI fill:#ffebee
```

## 6. Inter-Container Communication

### 6.1 Agent ↔ API
- **Protocolo**: HTTPS/JSON
- **Autenticação**: Certificate-based
- **Payload**: Compressed JSON
- **Frequência**: Configurable (default: 60s)

### 6.2 Web App ↔ API
- **Protocolo**: HTTPS/JSON
- **Autenticação**: JWT Bearer Token
- **Payload**: JSON REST
- **Comunicação**: Request/Response + WebSocket

### 6.3 API ↔ Databases
- **PostgreSQL**: Connection pooling, prepared statements
- **Redis**: Connection multiplexing, pub/sub
- **MinIO**: S3-compatible API, multipart uploads

### 6.4 Cross-Cutting Concerns
- **Logging**: Structured logging (Serilog)
- **Monitoring**: OpenTelemetry traces
- **Security**: HTTPS everywhere, input validation
- **Error Handling**: Global exception handlers
- **Caching**: Multi-level caching strategy

## 7. Deployment Architecture

```mermaid
graph TB
    subgraph "Corporate Network"
        subgraph "User Workstations"
            Agent1[EAM Agent<br/>Workstation 1]
            Agent2[EAM Agent<br/>Workstation 2]
            AgentN[EAM Agent<br/>Workstation N]
        end
        
        subgraph "Load Balancer"
            LB[Nginx<br/>Load Balancer]
        end
        
        subgraph "Application Servers"
            API1[EAM API<br/>Instance 1]
            API2[EAM API<br/>Instance 2]
            Web1[Web App<br/>Instance 1]
        end
        
        subgraph "Database Cluster"
            PGPrimary[PostgreSQL<br/>Primary]
            PGReplica[PostgreSQL<br/>Replica]
            RedisCluster[Redis<br/>Cluster]
            MinIOCluster[MinIO<br/>Cluster]
        end
        
        subgraph "Monitoring"
            Grafana[Grafana<br/>Dashboards]
            Loki[Loki<br/>Log Aggregation]
            Tempo[Tempo<br/>Trace Storage]
        end
    end
    
    Agent1 --> LB
    Agent2 --> LB
    AgentN --> LB
    
    LB --> API1
    LB --> API2
    LB --> Web1
    
    API1 --> PGPrimary
    API2 --> PGPrimary
    API1 --> RedisCluster
    API2 --> RedisCluster
    API1 --> MinIOCluster
    API2 --> MinIOCluster
    
    PGPrimary --> PGReplica
    
    API1 --> Grafana
    API2 --> Grafana
    Web1 --> Grafana
    
    style Agent1 fill:#e8f5e8
    style LB fill:#e3f2fd
    style API1 fill:#fff3e0
    style PGPrimary fill:#f3e5f5
```

## 8. Considerações de Arquitetura

### 8.1 Escalabilidade
- **Horizontal**: Múltiplas instâncias da API
- **Vertical**: Otimização de recursos
- **Database**: Read replicas, connection pooling
- **Cache**: Distributed caching com Redis

### 8.2 Disponibilidade
- **Load Balancing**: Nginx com health checks
- **Failover**: Automatic failover para replicas
- **Backup**: Automated backups dos dados
- **Monitoring**: Proactive monitoring e alertas

### 8.3 Segurança
- **Network**: HTTPS/TLS 1.3 everywhere
- **Authentication**: JWT + Active Directory
- **Authorization**: Role-based access control
- **Data**: Encryption at rest e in transit

### 8.4 Performance
- **Caching**: Multi-level caching strategy
- **Database**: Indexing otimizado
- **Compression**: Gzip compression
- **CDN**: Static assets delivery