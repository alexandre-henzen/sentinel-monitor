# EAM v5.0 - Deployment & CI/CD Documentation

Este diretório contém toda a infraestrutura de deployment e CI/CD para o Employee Activity Monitor (EAM) v5.0, incluindo pipelines, Dockerfiles, manifests Kubernetes, scripts de deployment e configurações de monitoramento.

## 📋 Índice

- [Visão Geral](#visão-geral)
- [Arquitetura de Deployment](#arquitetura-de-deployment)
- [Estrutura de Diretórios](#estrutura-de-diretórios)
- [Pipelines CI/CD](#pipelines-cicd)
- [Dockerfiles](#dockerfiles)
- [Kubernetes](#kubernetes)
- [Scripts de Deployment](#scripts-de-deployment)
- [Monitoramento](#monitoramento)
- [Guia de Deployment](#guia-de-deployment)
- [Troubleshooting](#troubleshooting)
- [Manutenção](#manutenção)

## 🎯 Visão Geral

O EAM v5.0 utiliza uma arquitetura moderna de deployment com:

- **Zero-downtime deployment** com rolling updates
- **Rollback automático** em caso de falha
- **Multi-stage builds** para otimização de containers
- **Security scanning** integrado no pipeline
- **Health checks** abrangentes
- **Backup automático** antes do deployment
- **Monitoramento completo** com alertas

## 🏗️ Arquitetura de Deployment

```
┌─────────────────────────────────────────────────────────────────┐
│                        CI/CD Pipeline                           │
├─────────────────────────────────────────────────────────────────┤
│  GitHub Actions          │  Azure DevOps                       │
│  ├─ Build & Test         │  ├─ Build & Test                    │
│  ├─ Security Scan        │  ├─ Security Scan                   │
│  ├─ Docker Build         │  ├─ Docker Build                    │
│  ├─ Deploy Staging       │  ├─ Deploy Staging                  │
│  └─ Deploy Production    │  └─ Deploy Production               │
└─────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Kubernetes Cluster                           │
├─────────────────────────────────────────────────────────────────┤
│  Nginx Ingress Controller                                       │
│  ├─ SSL Termination                                             │
│  ├─ Load Balancing                                              │
│  └─ Security Headers                                            │
│                                                                 │
│  Application Layer                                              │
│  ├─ EAM Frontend (Angular + Nginx)                             │
│  ├─ EAM API (.NET 8 + ASP.NET Core)                            │
│  └─ EAM Agent (Windows Service)                                │
│                                                                 │
│  Infrastructure Layer                                           │
│  ├─ PostgreSQL 16 (Database)                                   │
│  ├─ Redis 7 (Cache & Sessions)                                 │
│  ├─ MinIO (Object Storage)                                     │
│  └─ Monitoring Stack (Prometheus + Grafana)                    │
└─────────────────────────────────────────────────────────────────┘
```

## 📁 Estrutura de Diretórios

```
deploy/
├── .github/
│   └── workflows/
│       ├── ci-cd.yml              # Pipeline principal GitHub Actions
│       ├── security.yml           # Pipeline de segurança
│       └── release.yml            # Pipeline de release
├── azure-pipelines.yml            # Pipeline Azure DevOps
├── docker/
│   ├── agent.Dockerfile           # Dockerfile do Agent (Windows)
│   ├── api.Dockerfile             # Dockerfile da API (Linux)
│   └── frontend.Dockerfile        # Dockerfile do Frontend (Linux)
├── kubernetes/
│   ├── namespace.yml              # Namespace e políticas
│   ├── deployments/
│   │   ├── api-deployment.yml     # Deployment da API
│   │   └── frontend-deployment.yml # Deployment do Frontend
│   ├── services/
│   │   ├── api-service.yml        # Serviço da API
│   │   └── frontend-service.yml   # Serviço do Frontend
│   ├── ingress/
│   │   └── ingress.yml            # Ingress Controller
│   └── configmaps/
│       └── eam-config.yml         # Configurações da aplicação
├── scripts/
│   ├── deploy.sh                  # Script de deployment
│   ├── rollback.sh                # Script de rollback
│   ├── health-check.sh            # Script de health check
│   └── backup.sh                  # Script de backup
├── nginx/
│   ├── nginx.conf                 # Configuração principal do Nginx
│   ├── default.conf               # Configuração do servidor
│   ├── security-headers.conf      # Headers de segurança
│   └── mime.types                 # Tipos MIME
└── README.md                      # Esta documentação
```

## 🚀 Pipelines CI/CD

### GitHub Actions

#### Pipeline Principal (ci-cd.yml)

```yaml
Triggers:
- Push para main/develop
- Pull requests para main
- Releases

Jobs:
1. Build .NET (Windows)
2. Build Angular (Ubuntu)
3. Security Scan
4. Build Docker Images
5. Deploy to Staging
6. Deploy to Production
```

**Características:**
- **Paralelização**: Jobs independentes executam em paralelo
- **Caching**: Cache de dependências .NET e NPM
- **Artefatos**: Preservação de builds entre jobs
- **Segurança**: Scanning de vulnerabilidades
- **Deployment**: Zero-downtime com health checks

#### Pipeline de Segurança (security.yml)

```yaml
Scans:
- Dependency vulnerabilities (.NET/NPM)
- Code quality (SonarQube)
- Container security (Trivy)
- Secrets detection (TruffleHog)
- SAST analysis (CodeQL)
- License compliance
- DAST analysis (OWASP ZAP)
```

#### Pipeline de Release (release.yml)

```yaml
Triggers:
- Tags v*
- Manual dispatch

Features:
- Version validation
- Release artifacts
- Docker image tagging
- GitHub release creation
- Automatic deployment
```

### Azure DevOps

#### Pipeline Completo (azure-pipelines.yml)

```yaml
Stages:
1. Build and Test
2. Security Scan
3. Build Docker Images
4. Deploy to Staging
5. Deploy to Production
6. Post-deployment Tasks

Features:
- Multi-stage pipeline
- Approval gates
- Variable groups
- Service connections
- Artifact management
```

## 🐳 Dockerfiles

### EAM Agent (Windows Container)

```dockerfile
# Multi-stage build otimizado
FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022 AS build
# Build e publish com otimizações
FROM mcr.microsoft.com/windows/servercore:ltsc2022 AS runtime
# Configurações de segurança e health checks
```

**Características:**
- **Self-contained**: Inclui runtime .NET
- **Otimizado**: PublishTrimmed e ReadyToRun
- **Segurança**: Usuário não-root, read-only filesystem
- **Health checks**: Verificação de processo
- **Plugins**: Suporte a plugins dinâmicos

### EAM API (Linux Container)

```dockerfile
# Multi-stage build Alpine Linux
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
# Build otimizado
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
# Configurações de produção
```

**Características:**
- **Alpine Linux**: Imagem mínima e segura
- **Non-root user**: Execução com usuário dedicado
- **Health checks**: Endpoints de saúde
- **Environment**: Variáveis de ambiente
- **Volumes**: Dados temporários e logs

### EAM Frontend (Nginx + Angular)

```dockerfile
# Build stage com Node.js
FROM node:18-alpine AS build
# Runtime com Nginx otimizado
FROM nginx:alpine AS runtime
# Configurações de segurança e cache
```

**Características:**
- **Production build**: Otimizado para produção
- **Nginx**: Servidor web otimizado
- **Caching**: Headers de cache configurados
- **Security**: Headers de segurança
- **Compression**: Gzip habilitado

## ☸️ Kubernetes

### Namespace e Políticas

```yaml
# Namespace isolado
apiVersion: v1
kind: Namespace
metadata:
  name: eam-system

# Resource Quotas
# Limit Ranges
# Network Policies
```

### Deployments

#### API Deployment
- **Replicas**: 3 instâncias
- **Rolling Update**: Zero-downtime
- **Health Checks**: Liveness, Readiness, Startup
- **Resources**: CPU/Memory limits
- **Security**: SecurityContext configurado
- **Affinity**: Anti-affinity para disponibilidade

#### Frontend Deployment
- **Replicas**: 2 instâncias
- **Nginx**: Configuração otimizada
- **Static Files**: Caching configurado
- **Proxy**: Reverse proxy para API
- **Security**: Headers de segurança

### Services

#### API Service
- **Type**: ClusterIP
- **Ports**: HTTP/HTTPS
- **Health Checks**: Monitoring
- **Load Balancing**: Round-robin

#### Frontend Service
- **Type**: ClusterIP
- **Nginx**: Proxy reverso
- **SSL**: Terminação SSL
- **Monitoring**: Métricas Nginx

### Ingress Controller

```yaml
# Nginx Ingress com SSL
# Rate limiting
# CORS configurado
# Security headers
# Load balancing
```

**Características:**
- **SSL/TLS**: Certificados automáticos (Let's Encrypt)
- **Rate Limiting**: Proteção contra DDoS
- **CORS**: Cross-Origin Resource Sharing
- **Security Headers**: Headers de segurança
- **Monitoring**: Métricas Prometheus

### ConfigMaps

```yaml
# Configurações da aplicação
# Settings do Nginx
# Variáveis de ambiente
# Feature flags
```

## 📜 Scripts de Deployment

### deploy.sh

```bash
# Script principal de deployment
./deploy.sh production [version]

Options:
--dry-run      # Simular deployment
--force        # Forçar deployment
--skip-backup  # Pular backup
--verbose      # Log detalhado
```

**Funcionalidades:**
- **Pré-requisitos**: Verificação de dependências
- **Backup**: Backup automático antes do deploy
- **Zero-downtime**: Rolling updates
- **Health checks**: Verificação de saúde
- **Rollback**: Rollback automático em falha

### rollback.sh

```bash
# Script de rollback
./rollback.sh production [--revision N]

Options:
--revision N   # Rollback para revisão específica
--force        # Forçar rollback
--dry-run      # Simular rollback
```

**Funcionalidades:**
- **Histórico**: Visualização de revisões
- **Confirmação**: Confirmação antes do rollback
- **Automático**: Rollback para versão anterior
- **Verificação**: Health checks pós-rollback
- **Relatório**: Relatório de rollback

### health-check.sh

```bash
# Script de health check
./health-check.sh production

Options:
--component api     # Verificar componente específico
--output json       # Formato de saída
--verbose          # Log detalhado
```

**Verificações:**
- **Kubernetes**: Pods, Services, Deployments
- **API**: Endpoints de saúde
- **Frontend**: Páginas e proxy
- **Dependências**: PostgreSQL, Redis, MinIO
- **Métricas**: Formato Prometheus

### backup.sh

```bash
# Script de backup
./backup.sh production

Options:
--type database    # Tipo de backup
--compress        # Comprimir backup
--retention 30    # Retenção em dias
```

**Funcionalidades:**
- **Database**: Backup PostgreSQL
- **Configurações**: ConfigMaps e Secrets
- **Manifests**: Recursos Kubernetes
- **Compressão**: Arquivos comprimidos
- **Retenção**: Limpeza automática

## 📊 Monitoramento

### Health Checks

#### API Health Checks
```
GET /health        # Saúde geral
GET /health/ready  # Pronto para receber tráfego
GET /health/live   # Processo vivo
GET /health/db     # Conectividade database
```

#### Frontend Health Checks
```
GET /health        # Saúde do Nginx
GET /nginx_status  # Status do Nginx
```

### Métricas

#### Prometheus Metrics
```
# Métricas da aplicação
eam_api_requests_total
eam_api_request_duration_seconds
eam_database_connections_active
eam_cache_hits_total

# Métricas de sistema
container_cpu_usage_seconds_total
container_memory_usage_bytes
nginx_http_requests_total
```

### Alertas

#### Críticos
- **Pod Down**: Pod não está executando
- **High CPU**: CPU > 80% por 5 minutos
- **High Memory**: Memory > 90% por 5 minutos
- **Database Down**: Database não acessível

#### Avisos
- **High Response Time**: Tempo de resposta > 1s
- **Error Rate**: Taxa de erro > 5%
- **Disk Space**: Espaço em disco < 20%

## 🚀 Guia de Deployment

### Primeiro Deployment

1. **Preparação**:
   ```bash
   # Configurar kubeconfig
   kubectl config use-context production
   
   # Verificar cluster
   kubectl cluster-info
   ```

2. **Secrets**:
   ```bash
   # Criar secrets necessários
   kubectl create secret generic eam-secrets \
     --from-literal=database-password="..." \
     --from-literal=jwt-secret="..." \
     --from-literal=minio-access-key="..." \
     --from-literal=minio-secret-key="..."
   ```

3. **Deploy**:
   ```bash
   # Deployment completo
   ./deploy/scripts/deploy.sh production 5.0.0
   ```

### Deployment de Atualização

1. **Backup**:
   ```bash
   # Backup automático (incluído no deploy)
   ./deploy/scripts/backup.sh production
   ```

2. **Deploy**:
   ```bash
   # Deploy com nova versão
   ./deploy/scripts/deploy.sh production 5.0.1
   ```

3. **Verificação**:
   ```bash
   # Health check
   ./deploy/scripts/health-check.sh production
   ```

### Rollback

1. **Verificar Histórico**:
   ```bash
   kubectl rollout history deployment/eam-api
   kubectl rollout history deployment/eam-frontend
   ```

2. **Rollback**:
   ```bash
   # Rollback automático
   ./deploy/scripts/rollback.sh production
   
   # Rollback para revisão específica
   ./deploy/scripts/rollback.sh production --revision 5
   ```

## 🔧 Troubleshooting

### Problemas Comuns

#### 1. Pod não inicia
```bash
# Verificar logs
kubectl logs -f deployment/eam-api

# Verificar eventos
kubectl describe pod <pod-name>

# Verificar configurações
kubectl get configmap eam-config -o yaml
```

#### 2. Health Check falha
```bash
# Verificar endpoints
curl -f https://api.eam.company.com/health

# Verificar conectividade
kubectl exec -it <pod-name> -- wget -O- http://localhost:8080/health
```

#### 3. Database não conecta
```bash
# Verificar service
kubectl get svc postgresql-service

# Verificar connectivity
kubectl exec -it <api-pod> -- nc -zv postgresql-service 5432
```

#### 4. Ingress não funciona
```bash
# Verificar ingress
kubectl get ingress -o wide

# Verificar certificados
kubectl get certificates

# Verificar logs do ingress
kubectl logs -n ingress-nginx deployment/ingress-nginx-controller
```

### Logs e Debugging

#### Coleta de Logs
```bash
# Logs da API
kubectl logs -f deployment/eam-api --tail=100

# Logs do Frontend
kubectl logs -f deployment/eam-frontend --tail=100

# Logs do Ingress
kubectl logs -f -n ingress-nginx deployment/ingress-nginx-controller
```

#### Debug de Rede
```bash
# Teste de conectividade
kubectl exec -it <pod-name> -- ping eam-api-service

# Teste de DNS
kubectl exec -it <pod-name> -- nslookup eam-api-service
```

## 🛠️ Manutenção

### Backup Regular

```bash
# Backup diário (cron job)
0 2 * * * /path/to/deploy/scripts/backup.sh production --compress

# Backup antes de mudanças
./deploy/scripts/backup.sh production --type full
```

### Monitoramento de Recursos

```bash
# Verificar uso de recursos
kubectl top pods
kubectl top nodes

# Verificar HPA
kubectl get hpa
```

### Atualizações de Segurança

```bash
# Scan de vulnerabilidades
trivy image ghcr.io/company/eam-api:latest

# Atualizar imagens base
docker build --pull -t eam-api:latest -f deploy/docker/api.Dockerfile .
```

### Limpeza

```bash
# Limpar imagens antigas
docker image prune -a

# Limpar volumes não utilizados
docker volume prune

# Limpar recursos Kubernetes
kubectl delete pods --field-selector=status.phase=Succeeded
```

## 📚 Referências

- [Kubernetes Documentation](https://kubernetes.io/docs/)
- [Docker Best Practices](https://docs.docker.com/develop/dev-best-practices/)
- [Nginx Configuration](https://nginx.org/en/docs/)
- [GitHub Actions](https://docs.github.com/actions)
- [Azure DevOps](https://docs.microsoft.com/azure/devops/)

---

## 🆘 Suporte

Para suporte técnico:
- **Email**: devops@company.com
- **Slack**: #eam-deployment
- **Tickets**: [Sistema de Tickets](https://tickets.company.com)

**Documentação atualizada em**: $(date)
**Versão**: 5.0.0