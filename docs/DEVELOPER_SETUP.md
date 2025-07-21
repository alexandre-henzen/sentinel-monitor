# Guia de Setup para Desenvolvedores - EAM v5.0

Este guia fornece instruções detalhadas para configurar o ambiente de desenvolvimento do Employee Activity Monitor (EAM) v5.0.

## 📋 Pré-requisitos

### Ferramentas Obrigatórias

1. **Git**
   - Versão: 2.40+
   - Download: https://git-scm.com/downloads
   - Configuração inicial:
     ```bash
     git config --global user.name "Seu Nome"
     git config --global user.email "seu.email@empresa.com"
     ```

2. **.NET 8 SDK**
   - Versão: 8.0.0+
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Verificar instalação: `dotnet --version`

3. **Node.js**
   - Versão: 18.17+ (LTS recomendado)
   - Download: https://nodejs.org/
   - Verificar instalação: `node --version`

4. **npm**
   - Versão: 9.0+
   - Incluído com Node.js
   - Verificar instalação: `npm --version`

5. **Angular CLI**
   - Versão: 18.0+
   - Instalação: `npm install -g @angular/cli`
   - Verificar instalação: `ng version`

6. **Docker Desktop**
   - Versão: 4.20+
   - Download: https://www.docker.com/products/docker-desktop
   - Verificar instalação: `docker --version`

### Ferramentas Recomendadas

1. **Visual Studio Code**
   - Download: https://code.visualstudio.com/
   - Extensões recomendadas:
     - C# Dev Kit
     - Angular Language Service
     - Docker
     - GitLens
     - Prettier
     - ESLint
     - Thunder Client (para testes de API)

2. **Visual Studio 2022** (alternativa)
   - Edição: Community/Professional/Enterprise
   - Workloads: ASP.NET e desenvolvimento web, .NET desktop

3. **Postman** (para testes de API)
   - Download: https://www.postman.com/downloads/

### Banco de Dados (para desenvolvimento local)

1. **PostgreSQL 16+**
   - Download: https://www.postgresql.org/download/
   - Ou usar via Docker (recomendado)

2. **Redis 7+**
   - Download: https://redis.io/download
   - Ou usar via Docker (recomendado)

3. **MinIO** (para armazenamento de objetos)
   - Usar via Docker (recomendado)

## 🚀 Configuração Passo a Passo

### 1. Clone do Repositório

```bash
# Clone o repositório
git clone https://github.com/your-org/sentinel-monitor.git
cd sentinel-monitor

# Verificar se está na branch correta
git branch -a
git checkout main
```

### 2. Configuração Automatizada (Recomendado)

Execute o script de setup automatizado:

```powershell
# Windows PowerShell (Execute como Administrador)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
.\scripts\dev-setup.ps1
```

### 3. Configuração Manual (Alternativa)

#### 3.1 Configuração .NET

```bash
# Restaurar dependências .NET
dotnet restore

# Verificar se todos os projetos compilam
dotnet build

# Instalar ferramentas globais
dotnet tool install --global dotnet-ef
dotnet tool install --global dotnet-reportgenerator-globaltool
```

#### 3.2 Configuração Angular

```bash
# Navegar para o projeto Angular
cd src/Web/EAM.Web

# Instalar dependências
npm install

# Verificar se o projeto compila
ng build

# Voltar para a raiz
cd ../../../
```

#### 3.3 Configuração Docker

```bash
# Iniciar serviços de infraestrutura
docker-compose up -d postgres redis minio

# Verificar se os containers estão rodando
docker-compose ps

# Verificar logs (se necessário)
docker-compose logs postgres
docker-compose logs redis
docker-compose logs minio
```

### 4. Configuração do Banco de Dados

#### 4.1 Configurar Connection String

Edite o arquivo `src/API/EAM.API/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=eam_dev;Username=eam_user;Password=eam_password",
    "Redis": "localhost:6379"
  },
  "MinIO": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin"
  }
}
```

#### 4.2 Executar Migrações

```bash
# Navegar para o projeto da API
cd src/API/EAM.API

# Executar migrações
dotnet ef database update

# Voltar para a raiz
cd ../../../
```

### 5. Configuração de Ambiente

#### 5.1 Variáveis de Ambiente

Crie um arquivo `.env` na raiz do projeto:

```env
# Desenvolvimento
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=https://localhost:5001;http://localhost:5000

# Banco de Dados
DATABASE_URL=Host=localhost;Port=5432;Database=eam_dev;Username=eam_user;Password=eam_password
REDIS_URL=localhost:6379

# MinIO
MINIO_ENDPOINT=localhost:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin

# Logging
SEQ_SERVER_URL=http://localhost:5341
```

#### 5.2 Certificados SSL (para desenvolvimento)

```bash
# Gerar certificado de desenvolvimento
dotnet dev-certs https --trust
```

### 6. Executar a Aplicação

#### 6.1 Opção 1: Docker Compose (Recomendado)

```bash
# Executar toda a stack
docker-compose up

# Executar em background
docker-compose up -d

# Parar todos os serviços
docker-compose down
```

#### 6.2 Opção 2: Executar Individualmente

```bash
# Terminal 1 - Infraestrutura
docker-compose up -d postgres redis minio

# Terminal 2 - API
cd src/API/EAM.API
dotnet run

# Terminal 3 - Web
cd src/Web/EAM.Web
ng serve

# Terminal 4 - Agent (Windows)
cd src/Agent/EAM.Agent
dotnet run
```

## 🔧 Configuração da IDE

### Visual Studio Code

#### settings.json (workspace)

```json
{
  "dotnet.defaultSolution": "EAM.sln",
  "omnisharp.enableRoslynAnalyzers": true,
  "csharp.format.enable": true,
  "editor.formatOnSave": true,
  "editor.codeActionsOnSave": {
    "source.fixAll.eslint": true
  },
  "files.associations": {
    "*.csproj": "xml",
    "*.props": "xml",
    "*.targets": "xml"
  }
}
```

#### launch.json

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Launch EAM.API",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/API/EAM.API/bin/Debug/net8.0/EAM.API.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/API/EAM.API",
      "stopAtEntry": false,
      "serverReadyAction": {
        "action": "openExternally",
        "pattern": "\\bNow listening on:\\s+(https?://\\S+)"
      },
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    {
      "name": "Launch EAM.Agent",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/Agent/EAM.Agent/bin/Debug/net8.0/EAM.Agent.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/Agent/EAM.Agent",
      "stopAtEntry": false,
      "env": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  ]
}
```

### Visual Studio 2022

1. Abrir `EAM.sln`
2. Configurar múltiplos projetos de inicialização:
   - Solution Properties → Multiple Startup Projects
   - Definir EAM.API e EAM.Agent como "Start"
3. Configurar debugging com Docker:
   - Right-click no projeto → Add → Docker Support

## 🧪 Testes e Verificação

### Executar Testes

```bash
# Todos os testes .NET
dotnet test

# Testes com cobertura
dotnet test --collect:"XPlat Code Coverage"

# Testes Angular
cd src/Web/EAM.Web
npm test

# Testes E2E
npm run e2e
```

### Verificar Endpoints

#### API Health Checks
- Health: http://localhost:5000/health
- Ready: http://localhost:5000/health/ready
- Live: http://localhost:5000/health/live

#### Swagger/OpenAPI
- API Documentation: http://localhost:5000/swagger

#### Angular App
- Web App: http://localhost:4200

## 🛠️ Comandos Úteis

### .NET

```bash
# Limpar solução
dotnet clean

# Rebuild completo
dotnet build --no-restore

# Executar com watch (hot reload)
dotnet watch run --project src/API/EAM.API

# Adicionar pacote NuGet
dotnet add package PackageName

# Listar pacotes desatualizados
dotnet list package --outdated

# Atualizar pacotes
dotnet add package PackageName --version 1.0.0
```

### Angular

```bash
# Desenvolvimento com hot reload
ng serve

# Build para produção
ng build --prod

# Executar testes em watch mode
ng test --watch

# Gerar componente
ng generate component ComponentName

# Gerar serviço
ng generate service ServiceName

# Atualizar Angular
ng update
```

### Docker

```bash
# Rebuild imagens
docker-compose build --no-cache

# Logs de um serviço específico
docker-compose logs -f api

# Executar comando em container
docker-compose exec postgres psql -U eam_user -d eam_dev

# Limpar volumes
docker-compose down -v

# Limpar tudo
docker system prune -a
```

### Git

```bash
# Verificar status
git status

# Commit com conventional commits
git commit -m "feat: adicionar nova funcionalidade"

# Push com verificação de upstream
git push -u origin feature/branch-name

# Sync com main
git checkout main
git pull origin main
git checkout feature/branch-name
git rebase main
```

## 🐛 Troubleshooting

### Problemas Comuns

#### 1. Erro de Certificado SSL

```bash
# Solução
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

#### 2. Porta já em uso

```bash
# Verificar portas em uso
netstat -ano | findstr :5000
netstat -ano | findstr :4200

# Matar processo
taskkill /PID <process_id> /F
```

#### 3. Dependências não encontradas

```bash
# Limpar cache npm
npm cache clean --force

# Reinstalar dependências
rm -rf node_modules package-lock.json
npm install

# Limpar cache .NET
dotnet nuget locals all --clear
```

#### 4. Banco de dados não conecta

```bash
# Verificar se o container está rodando
docker ps

# Verificar logs do PostgreSQL
docker-compose logs postgres

# Recriar containers
docker-compose down
docker-compose up -d postgres
```

#### 5. Erro de migração

```bash
# Remover migração
dotnet ef migrations remove

# Recriar migração
dotnet ef migrations add InitialCreate

# Atualizar banco
dotnet ef database update
```

### Logs e Debugging

#### Habilitar logs detalhados

```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

#### Debugging com Chrome DevTools

```bash
# Angular com source maps
ng serve --source-map=true

# Abrir DevTools
F12 → Sources → webpack:// → src/app
```

## 📚 Recursos Adicionais

### Documentação
- [Documentação Arquitetural](../docs/architecture/)
- [.NET 8 Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [Angular Documentation](https://angular.io/docs)
- [Docker Documentation](https://docs.docker.com/)

### Ferramentas Online
- [Regex101](https://regex101.com/) - Testes de regex
- [JSON Formatter](https://jsonformatter.curiousconcept.com/) - Formatação JSON
- [Base64 Decode](https://www.base64decode.org/) - Decodificação Base64

### Padrões de Código
- [Conventional Commits](https://www.conventionalcommits.org/)
- [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Angular Style Guide](https://angular.io/guide/styleguide)

---

## 🆘 Suporte

Se encontrar problemas não cobertos neste guia:

1. Verifique as [Issues do GitHub](https://github.com/your-org/sentinel-monitor/issues)
2. Consulte a [documentação arquitetural](../docs/architecture/)
3. Abra uma nova issue com:
   - Passos para reproduzir
   - Mensagem de erro completa
   - Ambiente (OS, versões das ferramentas)
   - Logs relevantes

**Lembre-se**: Sempre trabalhe em branches separadas e faça commits pequenos e frequentes!