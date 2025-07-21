# Employee Activity Monitor (EAM) v5.0 - Estrutura da Solução

## Estrutura de Diretórios e Projetos

### Estrutura Geral
```
EAM/
├── docs/                              # Documentação
│   ├── architecture/                  # Documentação arquitetural
│   ├── api/                          # Documentação da API
│   └── deployment/                   # Guias de implantação
├── src/                              # Código fonte
│   ├── Agent/                        # Agente Windows
│   ├── Api/                          # API RESTful
│   ├── Web/                          # Frontend Angular
│   ├── Shared/                       # Bibliotecas compartilhadas
│   └── Infrastructure/               # Infraestrutura e utilitários
├── tests/                            # Testes
├── scripts/                          # Scripts de build/deployment
├── docker/                           # Containerização
└── tools/                            # Ferramentas de desenvolvimento
```

### Projetos da Solução (.NET)

#### 1. **Agente Windows (EAM.Agent)**
```
src/Agent/
├── EAM.Agent/                        # Aplicação principal do agente
│   ├── Services/                     # Serviços de captura
│   ├── Collectors/                   # Coletores de dados
│   ├── Configuration/                # Configurações
│   ├── Security/                     # Segurança e criptografia
│   └── Communication/                # Comunicação com API
├── EAM.Agent.Core/                   # Núcleo do agente
│   ├── Models/                       # Modelos de dados
│   ├── Interfaces/                   # Contratos
│   └── Utilities/                    # Utilitários
└── EAM.Agent.Installer/              # Instalador MSI
```

#### 2. **API RESTful (EAM.Api)**
```
src/Api/
├── EAM.Api/                          # API Web
│   ├── Controllers/                  # Controllers REST
│   ├── Middleware/                   # Middlewares
│   ├── Configuration/                # Configurações
│   └── Security/                     # Autenticação/Autorização
├── EAM.Api.Core/                     # Lógica de negócio
│   ├── Services/                     # Serviços de aplicação
│   ├── Domain/                       # Domínio
│   └── Interfaces/                   # Contratos
└── EAM.Api.Data/                     # Acesso a dados
    ├── Repositories/                 # Repositórios
    ├── Entities/                     # Entidades EF
    └── Migrations/                   # Migrações
```

#### 3. **Frontend Angular (EAM.Web)**
```
src/Web/
├── eam-web/                          # Aplicação Angular 18
│   ├── src/
│   │   ├── app/
│   │   │   ├── core/                 # Módulos core
│   │   │   ├── shared/               # Componentes compartilhados
│   │   │   ├── features/             # Funcionalidades
│   │   │   │   ├── dashboard/        # Dashboard
│   │   │   │   ├── reports/          # Relatórios
│   │   │   │   ├── settings/         # Configurações
│   │   │   │   └── users/            # Usuários
│   │   │   └── layouts/              # Layouts
│   │   ├── assets/                   # Recursos estáticos
│   │   └── environments/             # Ambientes
│   ├── angular.json
│   ├── package.json
│   └── tsconfig.json
```

#### 4. **Bibliotecas Compartilhadas**
```
src/Shared/
├── EAM.Shared/                       # Biblioteca base (já existe)
│   ├── Models/                       # DTOs e modelos
│   ├── Constants/                    # Constantes
│   ├── Enums/                        # Enumerações
│   └── Extensions/                   # Extensões
├── EAM.Shared.Contracts/             # Contratos e interfaces
├── EAM.Shared.Security/              # Segurança compartilhada
└── EAM.Shared.Telemetry/             # Telemetria compartilhada
```

#### 5. **Infraestrutura**
```
src/Infrastructure/
├── EAM.Infrastructure.Data/          # Infraestrutura de dados
│   ├── PostgreSQL/                   # Configurações PostgreSQL
│   ├── Redis/                        # Configurações Redis
│   └── MinIO/                        # Configurações MinIO
├── EAM.Infrastructure.Messaging/     # Mensageria
├── EAM.Infrastructure.Storage/       # Armazenamento
└── EAM.Infrastructure.Telemetry/     # OpenTelemetry
```

### Projetos de Teste
```
tests/
├── EAM.Agent.Tests/                  # Testes do agente
├── EAM.Api.Tests/                    # Testes da API
├── EAM.Web.Tests/                    # Testes do frontend
├── EAM.Integration.Tests/            # Testes de integração
└── EAM.Performance.Tests/            # Testes de performance
```

### Arquivos de Configuração da Solução
```
EAM/
├── EAM.sln                          # Solução Visual Studio
├── Directory.Build.props            # Propriedades globais
├── Directory.Build.targets          # Targets globais
├── global.json                      # Configuração .NET
├── nuget.config                     # Configuração NuGet
├── .editorconfig                    # Configuração de editor
├── .gitignore                       # Git ignore
└── README.md                        # Documentação principal
```

### Estrutura Docker
```
docker/
├── agent/                           # Dockerfile do agente
├── api/                             # Dockerfile da API
├── web/                             # Dockerfile do frontend
├── infrastructure/                  # Serviços de infraestrutura
│   ├── postgresql/
│   ├── redis/
│   ├── minio/
│   └── grafana/
├── docker-compose.yml               # Orquestração local
└── docker-compose.prod.yml          # Orquestração produção
```

## Tecnologias e Versões

### Backend (.NET 8)
- **Runtime**: .NET 8.0
- **Framework**: ASP.NET Core 8.0
- **ORM**: Entity Framework Core 8.0
- **Autenticação**: JWT + Identity
- **Validação**: FluentValidation
- **Mapeamento**: AutoMapper
- **Logging**: Serilog
- **Telemetria**: OpenTelemetry

### Frontend (Angular 18)
- **Framework**: Angular 18
- **UI**: Angular Material + PrimeNG
- **Estado**: NgRx (para funcionalidades complexas)
- **HTTP**: HttpClient + Interceptors
- **Gráficos**: Chart.js
- **Autenticação**: JWT + Guards

### Infraestrutura
- **Banco de Dados**: PostgreSQL 16
- **Cache**: Redis 7
- **Armazenamento**: MinIO
- **Observabilidade**: Grafana Loki/Tempo
- **Containerização**: Docker + Docker Compose
- **Reverse Proxy**: Nginx

### Desenvolvimento
- **Versionamento**: Git
- **CI/CD**: GitHub Actions
- **Testes**: xUnit, Moq, Testcontainers
- **Análise de Código**: SonarCloud
- **Documentação**: Markdown + Swagger/OpenAPI

## Considerações Arquiteturais

### Padrões Aplicados
- **Clean Architecture**: Separação clara entre camadas
- **CQRS**: Command Query Responsibility Segregation
- **Repository Pattern**: Abstração de acesso a dados
- **Dependency Injection**: Inversão de dependências
- **Options Pattern**: Configurações tipadas

### Segurança
- **Autenticação**: JWT com refresh tokens
- **Autorização**: Role-based + Claims-based
- **Criptografia**: AES-256 para dados sensíveis
- **HTTPS**: Obrigatório em produção
- **Rate Limiting**: Proteção contra abuso

### Performance
- **Caching**: Redis para dados frequentes
- **Pagination**: Para listagens grandes
- **Lazy Loading**: Carregamento sob demanda
- **Compression**: Gzip para responses HTTP
- **CDN**: Para recursos estáticos

### Escalabilidade
- **Horizontal**: Suporte a múltiplas instâncias
- **Vertical**: Otimização de recursos
- **Load Balancing**: Distribuição de carga
- **Database Sharding**: Preparação para crescimento
- **Microservices Ready**: Arquitetura modular