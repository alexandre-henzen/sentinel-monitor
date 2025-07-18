#!/bin/bash

# PostgreSQL Restore Script for EAM v5.0
# Restores database from backup with validation

set -e

# Configuration
POSTGRES_HOST="${POSTGRES_HOST:-localhost}"
POSTGRES_PORT="${POSTGRES_PORT:-5432}"
POSTGRES_DB="${POSTGRES_DB:-eam}"
POSTGRES_USER="${POSTGRES_USER:-eam_user}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-eam_pass}"
BACKUP_DIR="${BACKUP_DIR:-/backups}"
BACKUP_FILE="${1}"

# Function to show usage
show_usage() {
    echo "Usage: $0 <backup_file>"
    echo "Example: $0 /backups/eam_backup_20240101_120000.sql.gz"
    echo ""
    echo "Environment variables:"
    echo "  POSTGRES_HOST (default: localhost)"
    echo "  POSTGRES_PORT (default: 5432)"
    echo "  POSTGRES_DB (default: eam)"
    echo "  POSTGRES_USER (default: eam_user)"
    echo "  POSTGRES_PASSWORD (default: eam_pass)"
    echo "  BACKUP_DIR (default: /backups)"
    exit 1
}

# Check if backup file is provided
if [ -z "${BACKUP_FILE}" ]; then
    echo "❌ Erro: Arquivo de backup não especificado"
    show_usage
fi

# Check if backup file exists
if [ ! -f "${BACKUP_FILE}" ]; then
    echo "❌ Erro: Arquivo de backup não encontrado: ${BACKUP_FILE}"
    echo ""
    echo "📋 Backups disponíveis:"
    ls -lh "${BACKUP_DIR}"/eam_backup_*.sql.gz 2>/dev/null || echo "   Nenhum backup encontrado"
    exit 1
fi

echo "🔄 Iniciando restauração do PostgreSQL EAM v5.0..."
echo "   Host: ${POSTGRES_HOST}:${POSTGRES_PORT}"
echo "   Database: ${POSTGRES_DB}"
echo "   User: ${POSTGRES_USER}"
echo "   Backup file: ${BACKUP_FILE}"

# Set password for PostgreSQL commands
export PGPASSWORD="${POSTGRES_PASSWORD}"

# Verify backup integrity
echo "🔍 Verificando integridade do backup..."
if file "${BACKUP_FILE}" | grep -q "gzip"; then
    # If file is gzipped, decompress first
    TEMP_FILE="/tmp/$(basename "${BACKUP_FILE}" .gz)"
    gunzip -c "${BACKUP_FILE}" > "${TEMP_FILE}"
    RESTORE_FILE="${TEMP_FILE}"
else
    RESTORE_FILE="${BACKUP_FILE}"
fi

if ! pg_restore --list "${RESTORE_FILE}" > /dev/null 2>&1; then
    echo "❌ Erro: Backup corrompido ou inválido!"
    [ -f "${TEMP_FILE}" ] && rm -f "${TEMP_FILE}"
    exit 1
fi

echo "✅ Backup íntegro!"

# Create backup of current database before restore
CURRENT_BACKUP_FILE="${BACKUP_DIR}/eam_backup_before_restore_$(date +%Y%m%d_%H%M%S).sql.gz"
echo "💾 Criando backup da base atual..."
pg_dump \
  --host="${POSTGRES_HOST}" \
  --port="${POSTGRES_PORT}" \
  --username="${POSTGRES_USER}" \
  --dbname="${POSTGRES_DB}" \
  --no-password \
  --format=custom \
  --compress=6 \
  --file="${CURRENT_BACKUP_FILE%.gz}"

gzip -f "${CURRENT_BACKUP_FILE%.gz}"
echo "✅ Backup atual salvo: ${CURRENT_BACKUP_FILE}"

# Confirm restore operation
echo ""
echo "⚠️  ATENÇÃO: Esta operação irá substituir completamente a base de dados atual!"
echo "   Database: ${POSTGRES_DB}"
echo "   Backup atual salvo em: ${CURRENT_BACKUP_FILE}"
echo ""
read -p "Continuar com a restauração? (y/N): " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "❌ Operação cancelada pelo usuário"
    [ -f "${TEMP_FILE}" ] && rm -f "${TEMP_FILE}"
    exit 1
fi

# Drop existing connections
echo "🔌 Encerrando conexões existentes..."
psql \
  --host="${POSTGRES_HOST}" \
  --port="${POSTGRES_PORT}" \
  --username="${POSTGRES_USER}" \
  --dbname="postgres" \
  --no-password \
  --command="SELECT pg_terminate_backend(pg_stat_activity.pid) FROM pg_stat_activity WHERE pg_stat_activity.datname = '${POSTGRES_DB}' AND pid <> pg_backend_pid();"

# Drop and recreate database
echo "🗑️ Removendo database existente..."
psql \
  --host="${POSTGRES_HOST}" \
  --port="${POSTGRES_PORT}" \
  --username="${POSTGRES_USER}" \
  --dbname="postgres" \
  --no-password \
  --command="DROP DATABASE IF EXISTS ${POSTGRES_DB};"

echo "🆕 Criando database..."
psql \
  --host="${POSTGRES_HOST}" \
  --port="${POSTGRES_PORT}" \
  --username="${POSTGRES_USER}" \
  --dbname="postgres" \
  --no-password \
  --command="CREATE DATABASE ${POSTGRES_DB} OWNER ${POSTGRES_USER};"

# Restore database
echo "📥 Restaurando database..."
pg_restore \
  --host="${POSTGRES_HOST}" \
  --port="${POSTGRES_PORT}" \
  --username="${POSTGRES_USER}" \
  --dbname="${POSTGRES_DB}" \
  --no-password \
  --verbose \
  --clean \
  --if-exists \
  --no-owner \
  --no-privileges \
  "${RESTORE_FILE}"

# Verify restore
echo "🔍 Verificando restauração..."
TABLES_COUNT=$(psql -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'eam';" | tr -d ' ')
USERS_COUNT=$(psql -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -t -c "SELECT COUNT(*) FROM eam.users;" | tr -d ' ')
AGENTS_COUNT=$(psql -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -t -c "SELECT COUNT(*) FROM eam.agents;" | tr -d ' ')

echo "📊 Estatísticas após restauração:"
echo "   Tabelas: ${TABLES_COUNT}"
echo "   Usuários: ${USERS_COUNT}"
echo "   Agentes: ${AGENTS_COUNT}"

# Update sequences
echo "🔄 Atualizando sequences..."
psql \
  --host="${POSTGRES_HOST}" \
  --port="${POSTGRES_PORT}" \
  --username="${POSTGRES_USER}" \
  --dbname="${POSTGRES_DB}" \
  --no-password \
  --command="SELECT setval(pg_get_serial_sequence('eam.users', 'id'), COALESCE(MAX(id), 1)) FROM eam.users;"

# Refresh materialized views
echo "🔄 Atualizando views materializadas..."
psql \
  --host="${POSTGRES_HOST}" \
  --port="${POSTGRES_PORT}" \
  --username="${POSTGRES_USER}" \
  --dbname="${POSTGRES_DB}" \
  --no-password \
  --command="SELECT eam.refresh_all_materialized_views();" 2>/dev/null || echo "   Views materializadas não encontradas (normal para backups antigos)"

# Update statistics
echo "📈 Atualizando estatísticas..."
psql \
  --host="${POSTGRES_HOST}" \
  --port="${POSTGRES_PORT}" \
  --username="${POSTGRES_USER}" \
  --dbname="${POSTGRES_DB}" \
  --no-password \
  --command="ANALYZE;"

# Create restore report
REPORT_FILE="${BACKUP_DIR}/restore_report_$(date +%Y%m%d_%H%M%S).txt"
cat > "${REPORT_FILE}" << EOF
=== EAM v5.0 PostgreSQL Restore Report ===
Date: $(date)
Database: ${POSTGRES_DB}
Host: ${POSTGRES_HOST}:${POSTGRES_PORT}
User: ${POSTGRES_USER}
Backup file: ${BACKUP_FILE}

Restore Statistics:
- Tables: ${TABLES_COUNT}
- Users: ${USERS_COUNT}
- Agents: ${AGENTS_COUNT}

Backup Information:
- Original backup: ${CURRENT_BACKUP_FILE}
- Restored from: ${BACKUP_FILE}
- Backup size: $(du -h "${BACKUP_FILE}" | cut -f1)

Status: SUCCESS
EOF

echo "📄 Relatório criado: ${REPORT_FILE}"

# Clean up temporary files
[ -f "${TEMP_FILE}" ] && rm -f "${TEMP_FILE}"

echo ""
echo "✅ Restauração concluída com sucesso!"
echo "   Database: ${POSTGRES_DB}"
echo "   Backup original salvo em: ${CURRENT_BACKUP_FILE}"
echo "   Relatório: ${REPORT_FILE}"
echo ""
echo "🚨 IMPORTANTE: Reinicie a aplicação EAM para garantir que todas as conexões sejam renovadas."

# Unset password
unset PGPASSWORD