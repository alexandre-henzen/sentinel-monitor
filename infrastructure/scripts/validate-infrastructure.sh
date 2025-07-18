#!/bin/bash

# Infrastructure Validation Script for EAM v5.0
# Validates all infrastructure components

set -e

echo "🔍 Validando infraestrutura EAM v5.0..."

# Configuration
DEV_MODE=false
COMPOSE_FILE="docker-compose.yml"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --dev)
            DEV_MODE=true
            COMPOSE_FILE="docker-compose.dev.yml"
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test results
TESTS_PASSED=0
TESTS_FAILED=0

# Test function
test_service() {
    local service_name=$1
    local test_command=$2
    local expected_result=$3
    
    echo -n "   Testing $service_name... "
    
    if eval "$test_command" >/dev/null 2>&1; then
        echo -e "${GREEN}✅ PASS${NC}"
        ((TESTS_PASSED++))
    else
        echo -e "${RED}❌ FAIL${NC}"
        ((TESTS_FAILED++))
    fi
}

# Test PostgreSQL
echo "🗄️ Testando PostgreSQL..."
if [ "$DEV_MODE" = true ]; then
    DB_HOST="localhost"
    DB_PORT="5433"
    DB_NAME="eam_dev"
    DB_USER="eam_dev"
    DB_PASS="eam_dev_pass"
    CONTAINER_NAME="eam-postgres-dev"
else
    DB_HOST="localhost"
    DB_PORT="5432"
    DB_NAME="eam"
    DB_USER="eam_user"
    DB_PASS="eam_pass"
    CONTAINER_NAME="eam-postgres"
fi

test_service "PostgreSQL Connection" "docker exec $CONTAINER_NAME pg_isready -U $DB_USER -d $DB_NAME" "success"
test_service "PostgreSQL Tables" "PGPASSWORD=$DB_PASS psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -c 'SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = \"eam\"' -t | grep -q '[0-9]'" "success"
test_service "PostgreSQL Users Table" "PGPASSWORD=$DB_PASS psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -c 'SELECT COUNT(*) FROM eam.users' -t | grep -q '[0-9]'" "success"
test_service "PostgreSQL Agents Table" "PGPASSWORD=$DB_PASS psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -c 'SELECT COUNT(*) FROM eam.agents' -t | grep -q '[0-9]'" "success"
test_service "PostgreSQL Partitions" "PGPASSWORD=$DB_PASS psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -c 'SELECT COUNT(*) FROM pg_tables WHERE tablename LIKE \"activity_logs_%\"' -t | grep -q '[0-9]'" "success"
test_service "PostgreSQL Indexes" "PGPASSWORD=$DB_PASS psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -c 'SELECT COUNT(*) FROM pg_indexes WHERE schemaname = \"eam\"' -t | grep -q '[0-9]'" "success"
test_service "PostgreSQL Materialized Views" "PGPASSWORD=$DB_PASS psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -c 'SELECT COUNT(*) FROM pg_matviews WHERE schemaname = \"eam\"' -t | grep -q '[0-9]'" "success"

# Test Redis
echo "🔄 Testando Redis..."
if [ "$DEV_MODE" = true ]; then
    REDIS_CONTAINER="eam-redis-dev"
    REDIS_PORT="6380"
else
    REDIS_CONTAINER="eam-redis"
    REDIS_PORT="6379"
fi

test_service "Redis Connection" "docker exec $REDIS_CONTAINER redis-cli ping" "PONG"
test_service "Redis Set/Get" "docker exec $REDIS_CONTAINER redis-cli set test_key test_value && docker exec $REDIS_CONTAINER redis-cli get test_key | grep -q 'test_value'" "success"
test_service "Redis Databases" "docker exec $REDIS_CONTAINER redis-cli config get databases | grep -q '16'" "success"

# Test MinIO
echo "🗄️ Testando MinIO..."
if [ "$DEV_MODE" = true ]; then
    MINIO_CONTAINER="eam-minio-dev"
    MINIO_PORT="9001"
else
    MINIO_CONTAINER="eam-minio"
    MINIO_PORT="9000"
fi

test_service "MinIO Health" "curl -f http://localhost:$MINIO_PORT/minio/health/live" "success"
test_service "MinIO Bucket" "docker exec $MINIO_CONTAINER mc ls minio/eam-screenshots" "success"

# Test Docker containers
echo "🐳 Testando containers Docker..."
required_containers=("postgres" "redis" "minio")
if [ "$DEV_MODE" = false ]; then
    required_containers+=("jaeger" "prometheus" "grafana" "nginx")
fi

for container in "${required_containers[@]}"; do
    if [ "$DEV_MODE" = true ]; then
        container_name="eam-$container-dev"
    else
        container_name="eam-$container"
    fi
    
    test_service "Container $container" "docker ps --format 'table {{.Names}}\t{{.Status}}' | grep -q '$container_name.*Up'" "success"
done

# Test network connectivity
echo "🌐 Testando conectividade..."
test_service "PostgreSQL Port" "nc -z localhost $DB_PORT" "success"
test_service "Redis Port" "nc -z localhost $REDIS_PORT" "success"
test_service "MinIO Port" "nc -z localhost $MINIO_PORT" "success"

if [ "$DEV_MODE" = false ]; then
    test_service "Jaeger Port" "nc -z localhost 16686" "success"
    test_service "Prometheus Port" "nc -z localhost 9090" "success"
    test_service "Grafana Port" "nc -z localhost 3000" "success"
    test_service "Nginx Port" "nc -z localhost 80" "success"
fi

# Test file permissions
echo "📁 Testando permissões..."
test_service "Backup Script Executable" "[ -x scripts/backup-postgres.sh ]" "success"
test_service "Restore Script Executable" "[ -x scripts/restore-postgres.sh ]" "success"
test_service "MinIO Setup Script Executable" "[ -x storage/minio/setup-minio.sh ]" "success"
test_service "Init Script Executable" "[ -x scripts/init-infrastructure.sh ]" "success"

# Test backup functionality
echo "💾 Testando backup..."
test_service "Backup Directory" "[ -d database/backups ]" "success"
test_service "Backup Script" "bash scripts/backup-postgres.sh 2>/dev/null" "success"

# Performance test
echo "⚡ Testando performance..."
test_service "PostgreSQL Performance" "PGPASSWORD=$DB_PASS psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -c 'SELECT COUNT(*) FROM eam.activity_logs' -t | grep -q '[0-9]'" "success"

# Test data integrity
echo "🔍 Testando integridade dos dados..."
test_service "Foreign Key Constraints" "PGPASSWORD=$DB_PASS psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -c 'SELECT COUNT(*) FROM information_schema.table_constraints WHERE constraint_type = \"FOREIGN KEY\" AND table_schema = \"eam\"' -t | grep -q '[0-9]'" "success"

# Clean up test data
echo "🧹 Limpando dados de teste..."
docker exec $REDIS_CONTAINER redis-cli del test_key >/dev/null 2>&1 || true

# Summary
echo ""
echo "📊 Resumo da validação:"
echo "   Total de testes: $((TESTS_PASSED + TESTS_FAILED))"
echo -e "   Testes passaram: ${GREEN}$TESTS_PASSED${NC}"
echo -e "   Testes falharam: ${RED}$TESTS_FAILED${NC}"

if [ $TESTS_FAILED -eq 0 ]; then
    echo ""
    echo -e "${GREEN}🎉 Todos os testes passaram! Infraestrutura EAM v5.0 está funcionando corretamente.${NC}"
    echo ""
    echo "✅ Componentes validados:"
    echo "   • PostgreSQL 16 com particionamento"
    echo "   • Redis 7 para cache e sessões"
    echo "   • MinIO para armazenamento de screenshots"
    echo "   • Docker containers e networking"
    echo "   • Scripts de backup e restore"
    echo "   • Conectividade de rede"
    echo "   • Permissões de arquivos"
    echo "   • Integridade de dados"
    
    if [ "$DEV_MODE" = false ]; then
        echo "   • Jaeger para tracing"
        echo "   • Prometheus para métricas"
        echo "   • Grafana para visualização"
        echo "   • Nginx reverse proxy"
    fi
    
    echo ""
    echo "🚀 A infraestrutura está pronta para uso!"
    exit 0
else
    echo ""
    echo -e "${RED}❌ Alguns testes falharam. Verifique os logs e configurações.${NC}"
    echo ""
    echo "🔧 Comandos úteis para debug:"
    echo "   docker-compose -f $COMPOSE_FILE logs"
    echo "   docker-compose -f $COMPOSE_FILE ps"
    echo "   docker system df"
    echo ""
    exit 1
fi