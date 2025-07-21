# Employee Activity Monitor (EAM) v5.0

[![Build Status](https://github.com/your-org/sentinel-monitor/workflows/CI-CD/badge.svg)](https://github.com/your-org/sentinel-monitor/actions)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Angular Version](https://img.shields.io/badge/Angular-18-red.svg)](https://angular.io/)

## 📋 Visão Geral

O Employee Activity Monitor (EAM) v5.0 é uma solução completa e moderna para monitoramento de atividades de funcionários em ambientes corporativos. A solução foi projetada com arquitetura limpa, escalabilidade e observabilidade em mente.

### 🎯 Principais Funcionalidades

- **Monitoramento em Tempo Real**: Coleta contínua de dados de atividade dos funcionários
- **Dashboard Intuitivo**: Interface web moderna construída com Angular 18
- **API RESTful**: Serviços robustos desenvolvidos em ASP.NET Core 8
- **Agente Windows**: Serviço nativo para coleta de dados no Windows
- **Observabilidade**: Logging estruturado, métricas e rastreamento distribuído
- **Containerização**: Suporte completo ao Docker para deployment
- **CI/CD**: Pipeline automatizado com GitHub Actions

## 🏗️ Arquitetura

A solução segue os princípios da Arquitetura Limpa e está organizada nos seguintes componentes:

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   EAM.Web       │    │    EAM.API       │    │   EAM.Agent     │
│   (Angular 18)  │◄──►│  (ASP.NET Core)  │◄──►│ (Windows Svc)   │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │
                                ▼
                        ┌──────────────────┐
                        │   EAM.Shared     │
                        │   (Bibliotecas)  │
                        └──────────────────┘
```

### 🧩 Componentes Principais

- **EAM.Web**: SPA Angular 18 com Material Design
- **EAM.API**: Web API ASP.NET Core 8 com padrões RESTful
- **EAM.Agent**: Windows Service para coleta de dados
- **EAM.Shared**: Bibliotecas compartilhadas e utilitários
- **EAM.Tests**: Testes unitários e de integração

## 🔧 Requisitos do Sistema

### Desenvolvimento
- .NET 8 SDK
- Node.js 18+ e npm
- Angular CLI 18+
- Docker Desktop
- Visual Studio Code ou Visual Studio 2022
- Git

### Produção
- Windows Server 2019+ ou Linux (Docker)
- PostgreSQL 16+
- Redis 7+
- MinIO (para armazenamento de objetos)

## 🚀 Setup do Ambiente de Desenvolvimento

### 1. Clone o Repositório
```bash
git clone https://github.com/your-org/sentinel-monitor.git
cd sentinel-monitor
```

### 2. Configuração Automatizada
Execute o script de setup para configurar todo o ambiente:

```powershell
# Windows PowerShell
.\scripts\dev-setup.ps1

# ou manualmente:
# Instalar dependências .NET
dotnet restore

# Instalar dependências Angular
cd src/Web/EAM.Web
npm install
cd ../../../

# Configurar banco de dados (Docker)
docker-compose up -d postgres redis minio
```

### 3. Configuração do Banco de Dados
```bash
# Execute as migrações
dotnet ef database update --project src/API/EAM.API
```

### 4. Executar a Solução
```bash
# Opção 1: Usando Docker Compose (recomendado)
docker-compose up

# Opção 2: Executar individualmente
# Terminal 1 - API
cd src/API/EAM.API
dotnet run

# Terminal 2 - Web
cd src/Web/EAM.Web
ng serve

# Terminal 3 - Agent (Windows)
cd src/Agent/EAM.Agent
dotnet run
```

## 📁 Estrutura do Projeto

```
sentinel-monitor/
├── docs/                           # Documentação arquitetural
│   └── architecture/               # Diagramas e especificações
├── src/                            # Código fonte
│   ├── Agent/                      # Componentes do Agent
│   │   ├── EAM.Agent/             # Windows Service
│   │   ├── EAM.Agent.Core/        # Lógica de negócio
│   │   └── EAM.Agent.Infrastructure/ # Infraestrutura
│   ├── API/                       # Componentes da API
│   │   ├── EAM.API/              # Web API
│   │   ├── EAM.API.Core/         # Lógica de negócio
│   │   └── EAM.API.Infrastructure/ # Infraestrutura
│   ├── Web/                       # Componentes Web
│   │   └── EAM.Web/              # Angular SPA
│   ├── EAM.Shared/               # Bibliotecas compartilhadas
│   └── EAM.Tests/                # Testes
├── scripts/                       # Scripts de automação
├── .github/                       # GitHub Actions workflows
├── docker-compose.yml             # Orquestração Docker
└── EAM.sln                       # Solução Visual Studio
```

## 🧪 Testes

### Executar Todos os Testes
```bash
# Testes .NET
dotnet test

# Testes Angular
cd src/Web/EAM.Web
npm test

# Testes E2E
npm run e2e
```

### Cobertura de Código
```bash
# Gerar relatório de cobertura
dotnet test --collect:"XPlat Code Coverage"

# Visualizar relatório
cd TestResults/
reportgenerator -reports:coverage.cobertura.xml -targetdir:html
```

## 📊 Monitoramento e Observabilidade

### Logging
- **Serilog**: Logging estruturado
- **OpenTelemetry**: Rastreamento distribuído
- **Seq**: Agregação de logs (desenvolvimento)

### Métricas
- **Prometheus**: Coleta de métricas
- **Grafana**: Visualização de métricas

### Saúde da Aplicação
- Health checks habilitados em todos os componentes
- Endpoints: `/health`, `/health/ready`, `/health/live`

## 🔐 Segurança

### Autenticação e Autorização
- JWT Bearer tokens
- Role-based access control (RBAC)
- OAuth 2.0 / OpenID Connect ready

### Proteção de Dados
- Criptografia de dados sensíveis
- Auditoria de acesso
- Compliance com LGPD/GDPR

## 🚢 Deployment

### Docker
```bash
# Build de todas as imagens
docker-compose build

# Deploy em produção
docker-compose -f docker-compose.prod.yml up -d
```

### CI/CD
- Pipeline automatizado com GitHub Actions
- Build, test, security scan e deploy
- Ambientes: Development, Staging, Production

## 🤝 Contribuindo

1. Fork o projeto
2. Crie uma branch para sua feature (`git checkout -b feature/AmazingFeature`)
3. Commit suas mudanças (`git commit -m 'Add some AmazingFeature'`)
4. Push para a branch (`git push origin feature/AmazingFeature`)
5. Abra um Pull Request

### Padrões de Código
- Siga o `.editorconfig`
- Use conventional commits
- Mantenha cobertura de testes > 80%
- Documente APIs com OpenAPI/Swagger

## 📝 Roadmap

### Fase 0: Bootstrap ✅
- [x] Estrutura base da solução
- [x] Configuração de projetos
- [x] CI/CD pipeline
- [x] Containerização

### Fase 1: Funcionalidades Core (Em Planejamento)
- [ ] Autenticação e autorização
- [ ] Coleta básica de dados
- [ ] Dashboard inicial
- [ ] APIs fundamentais

### Fase 2: Funcionalidades Avançadas (Futuro)
- [ ] Relatórios avançados
- [ ] Integrações externas
- [ ] Machine Learning
- [ ] Mobile app

## 📄 Licença

Este projeto está licenciado sob a Licença MIT - veja o arquivo [LICENSE](LICENSE) para detalhes.

## 📞 Suporte

- **Documentação**: [docs/architecture/](docs/architecture/)
- **Issues**: [GitHub Issues](https://github.com/your-org/sentinel-monitor/issues)
- **Discussions**: [GitHub Discussions](https://github.com/your-org/sentinel-monitor/discussions)

---

**Desenvolvido com ❤️ pela equipe EAM**