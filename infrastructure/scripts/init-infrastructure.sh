#!/bin/bash

# Infrastructure Initialization Script for EAM v5.0
# Sets up the complete infrastructure environment

set -e

echo "🚀 Iniciando infraestrutura EAM v5.0..."

# Configuration
COMPOSE_FILE="docker-compose.yml"
DEV_MODE=false
SKIP_SEED=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --dev)
            DEV_MODE=true
            COMPOSE_FILE="docker-compose.dev.yml"
            shift
            ;;
        --skip-seed)
            SKIP_SEED=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo "Options:"
            echo "  --dev          Use development environment"
            echo "  --skip-seed    Skip database seeding"
            echo "  -h, --help     Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Create necessary directories
echo "📁 Criando diretórios necessários..."
mkdir -p database/backups
mkdir -p storage/minio/data
mkdir -p storage/redis/data
mkdir -p nginx/logs
mkdir -p monitoring/grafana/data
mkdir -p monitoring/prometheus/data

# Set permissions
chmod +x scripts/*.sh
chmod +x storage/minio/setup-minio.sh

# Stop existing containers
echo "🛑 Parando containers existentes..."
docker-compose -f $COMPOSE_FILE down --remove-orphans 2>/dev/null || true

# Remove old volumes if in dev mode
if [ "$DEV_MODE" = true ]; then
    echo "🧹 Removendo volumes de desenvolvimento..."
    docker volume rm eam-postgres-dev-data eam-redis-dev-data eam-minio-dev-data 2>/dev/null || true
fi

# Pull latest images
echo "📦 Atualizando imagens Docker..."
docker-compose -f $COMPOSE_FILE pull

# Start infrastructure services
echo "🔧 Iniciando serviços de infraestrutura..."
docker-compose -f $COMPOSE_FILE up -d postgres redis minio

# Wait for services to be ready
echo "⏳ Aguardando serviços estarem prontos..."
sleep 30

# Check PostgreSQL
echo "🔍 Verificando PostgreSQL..."
until docker exec eam-postgres${DEV_MODE:+-dev} pg_isready -U eam_user -d eam 2>/dev/null; do
    echo "   PostgreSQL não está pronto, aguardando..."
    sleep 5
done
echo "✅ PostgreSQL pronto!"

# Check Redis
echo "🔍 Verificando Redis..."
until docker exec eam-redis${DEV_MODE:+-dev} redis-cli ping 2>/dev/null; do
    echo "   Redis não está pronto, aguardando..."
    sleep 5
done
echo "✅ Redis pronto!"

# Check MinIO
echo "🔍 Verificando MinIO..."
until curl -f http://localhost:9000/minio/health/live 2>/dev/null; do
    echo "   MinIO não está pronto, aguardando..."
    sleep 5
done
echo "✅ MinIO pronto!"

# Setup database
echo "🗄️ Configurando banco de dados..."
if [ "$DEV_MODE" = true ]; then
    DB_HOST="localhost"
    DB_PORT="5433"
    DB_NAME="eam_dev"
    DB_USER="eam_dev"
    DB_PASS="eam_dev_pass"
else
    DB_HOST="localhost"
    DB_PORT="5432"
    DB_NAME="eam"
    DB_USER="eam_user"
    DB_PASS="eam_pass"
fi

# Execute database setup
PGPASSWORD=$DB_PASS psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -f setup-database.sql

# Setup MinIO
echo "🗄️ Configurando MinIO..."
docker-compose -f $COMPOSE_FILE up -d minio-setup

# Wait for MinIO setup to complete
sleep 10

# Start remaining services
echo "🔧 Iniciando serviços restantes..."
docker-compose -f $COMPOSE_FILE up -d

# Wait for all services
echo "⏳ Aguardando todos os serviços..."
sleep 15

# Health check
echo "🔍 Verificando saúde dos serviços..."
services=("postgres" "redis" "minio")
if [ "$DEV_MODE" = false ]; then
    services+=("jaeger" "prometheus" "grafana" "nginx")
fi

for service in "${services[@]}"; do
    if docker-compose -f $COMPOSE_FILE ps $service | grep -q "Up"; then
        echo "✅ $service está rodando"
    else
        echo "❌ $service não está rodando"
    fi
done

# Show service URLs
echo ""
echo "🌐 Serviços disponíveis:"
if [ "$DEV_MODE" = true ]; then
    echo "   PostgreSQL: localhost:5433"
    echo "   Redis: localhost:6380"
    echo "   MinIO: http://localhost:9001"
    echo "   PgAdmin: http://localhost:5050"
    echo "   Redis Commander: http://localhost:8081"
    echo "   Jaeger: http://localhost:16687"
else
    echo "   PostgreSQL: localhost:5432"
    echo "   Redis: localhost:6379"
    echo "   MinIO: http://localhost:9000"
    echo "   Jaeger: http://localhost:16686"
    echo "   Prometheus: http://localhost:9090"
    echo "   Grafana: http://localhost:3000"
    echo "   Nginx: http://localhost:80"
fi

# Show logs
echo ""
echo "📋 Para visualizar logs:"
echo "   docker-compose -f $COMPOSE_FILE logs -f"
echo ""
echo "🔧 Para parar os serviços:"
echo "   docker-compose -f $COMPOSE_FILE down"
echo ""

# Final status
echo "✅ Infraestrutura EAM v5.0 iniciada com sucesso!"
echo ""
echo "🎯 Próximos passos:"
echo "1. Configure as connection strings da aplicação"
echo "2. Execute os testes de conectividade"
echo "3. Inicie a aplicação EAM"
echo ""
echo "📖 Consulte o README.md para instruções detalhadas"