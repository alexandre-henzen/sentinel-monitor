# Employee Activity Monitor (EAM) v5.0

Sistema completo de monitoramento de atividades de funcionários com agente Windows, API RESTful e sistema de plugins extensível.

## 🏗️ Arquitetura

- **EAM.Agent**: Windows Service (.NET 8) com trackers de atividade
- **EAM.API**: API RESTful (ASP.NET Core 8) com PostgreSQL
- **EAM.Shared**: Biblioteca compartilhada com modelos e DTOs
- **EAM.PluginSDK**: SDK para desenvolvimento de plugins
- **EAM.Web**: Frontend Angular 18 (próxima fase)

## 🚀 Funcionalidades Implementadas

### EAM.Agent - Windows Service
- ✅ **WindowTracker**: Captura janelas em foco (1s)
- ✅ **BrowserTracker**: Detecta URLs de navegadores (2s)
- ✅ **TeamsTracker**: Monitora status do Microsoft Teams (5s)
- ✅ **ScreenshotCapturer**: Captura screenshots comprimidos (60s)
- ✅ **ProcessMonitor**: Monitora processos iniciados/terminados (5s)
- ✅ **Cache SQLite**: Armazenamento local offline
- ✅ **Sincronização NDJSON**: Sync com API a cada 60s
- ✅ **Sistema de Plugins**: Carregamento dinâmico com isolamento
- ✅ **Scoring de Produtividade**: Categorização configurável
- ✅ **Telemetria OpenTelemetry**: OTLP gRPC porta 4317

### EAM.API - RESTful API
- ✅ **Endpoints de Eventos**: Ingestão NDJSON em lotes
- ✅ **Gerenciamento de Agentes**: Registro e heartbeat
- ✅ **Autenticação JWT**: Escopo `eam.agent`
- ✅ **Armazenamento MinIO**: Screenshots e arquivos
- ✅ **Database PostgreSQL**: Persistência com Entity Framework
- ✅ **Cache Redis**: Performance e sessões
- ✅ **Health Checks**: Monitoramento de saúde
- ✅ **Middleware**: Logging e tratamento de erros

### EAM.Shared - Biblioteca Compartilhada
- ✅ **Modelos de Domínio**: Agent, ActivityLog, User, DailyScore
- ✅ **DTOs**: EventDto, AgentDto, ScreenshotDto
- ✅ **Enums**: AgentStatus, ActivityType
- ✅ **Constants**: ApiEndpoints centralizados

### EAM.PluginSDK - SDK para Plugins
- ✅ **Interface ITrackerPlugin**: Contrato para plugins
- ✅ **ActivityEvent**: Modelo de eventos
- ✅ **PluginAssemblyLoadContext**: Isolamento de assemblies
- ✅ **Carregamento Dinâmico**: Hot-reload de plugins

## 🔧 Configuração

### Pré-requisitos
- .NET 8 SDK
- PostgreSQL 16+
- Redis 7+
- MinIO (opcional)

### Instalação
```bash
# Clonar e compilar
git clone <repository>
cd sentinel-monitor
./build.ps1

# Configurar banco de dados
# Editar connection strings em appsettings.json
```

### Configuração do Agente
```json
{
  "Agent": {
    "ApiBaseUrl": "https://api.eam.local",
    "SyncIntervalSeconds": 60
  },
  "Trackers": {
    "WindowTracker": { "Enabled": true, "IntervalSeconds": 1 },
    "ScreenshotCapturer": { "Enabled": true, "IntervalSeconds": 60 }
  },
  "Scoring": {
    "Categories": {
      "Productive": ["outlook", "teams", "vscode"],
      "Unproductive": ["games", "social"]
    }
  }
}
```

## 📊 Intervalos de Captura

| Tracker | Intervalo | Descrição |
|---------|-----------|-----------|
| WindowTracker | 1s | Janelas em foco |
| BrowserTracker | 2s | URLs de navegadores |
| TeamsTracker | 5s | Status do Teams |
| ScreenshotCapturer | 60s | Screenshots JPEG |
| ProcessMonitor | 5s | Processos |
| Sync | 60s | Sincronização API |

## 🔌 Sistema de Plugins

### Diretório de Plugins
```
%PROGRAMDATA%\EAM\plugins\
├── MyPlugin\
│   ├── MyPlugin.dll
│   ├── plugin.json
│   └── config.json
```

### Exemplo de Plugin
```csharp
public class MyPlugin : ITrackerPlugin
{
    public string Name => "MyPlugin";
    public string Version => "1.0.0";
    
    public async ValueTask InitializeAsync(IConfiguration config, ILogger logger)
    {
        // Inicialização
    }
    
    public async ValueTask<IEnumerable<ActivityEvent>> PollAsync(CancellationToken ct)
    {
        // Captura de eventos
        return new[] { new ActivityEvent("MyEvent") };
    }
}
```

## 🔐 Segurança

- **Autenticação JWT**: Tokens com escopo `eam.agent`
- **HTTPS**: Comunicação criptografada
- **Isolamento**: Plugins em contextos separados
- **Validação**: FluentValidation em APIs

## 📈 Telemetria

- **OpenTelemetry**: Traces, métricas e logs
- **OTLP gRPC**: Exportação para Grafana/Jaeger
- **Métricas**: Eventos processados, sync, erros
- **Health Checks**: Status de componentes

## 🛠️ Build e Deploy

```bash
# Build completo
./build.ps1 -Configuration Release

# Apenas Agent
dotnet publish src/EAM.Agent/EAM.Agent.csproj -c Release -r win-x64 --self-contained

# Apenas API
dotnet publish src/EAM.API/EAM.API.csproj -c Release

# Instalar como serviço Windows
sc create "EAM Agent" binPath="C:\Path\To\EAM.Agent.exe"
```

## 🔄 Próximas Fases

- [ ] **EAM.Web**: Frontend Angular 18 com PrimeNG
- [ ] **Testes**: Unitários e integração
- [ ] **Docker**: Containerização completa
- [ ] **CI/CD**: GitHub Actions
- [ ] **Monitoring**: Grafana dashboards

## 📁 Estrutura de Arquivos

```
EAM.sln
├── src/
│   ├── EAM.Agent/           # Windows Service
│   ├── EAM.API/             # RESTful API
│   └── EAM.Shared/          # Biblioteca compartilhada
├── tools/
│   ├── EAM.PluginSDK/       # SDK para plugins
│   └── EAM.Installer/       # MSI Installer (futuro)
├── tests/                   # Testes (futuro)
├── infrastructure/          # Docker, K8s (futuro)
└── build.ps1               # Script de build
```

## 🎯 Status do Projeto

- ✅ **Arquitetura Base**: Completa e funcional
- ✅ **Agente Windows**: Trackers implementados
- ✅ **API RESTful**: Endpoints básicos
- ✅ **Sistema de Plugins**: Funcional
- ✅ **Telemetria**: OpenTelemetry configurado
- ⏸️ **Frontend**: Próxima fase
- ⏸️ **Testes**: Próxima fase

## 📝 Licença

Copyright © 2025 EAM Systems. Todos os direitos reservados.