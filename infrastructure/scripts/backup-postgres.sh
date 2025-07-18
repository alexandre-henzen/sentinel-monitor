#!/bin/bash

# PostgreSQL Backup Script for EAM v5.0
# Creates full database backup with compression and rotation

set -e

# Configuration
POSTGRES_HOST="${POSTGRES_HOST:-localhost}"
POSTGRES_PORT="${POSTGRES_PORT:-5432}"
POSTGRES_DB="${POSTGRES_DB:-eam}"
POSTGRES_USER="${POSTGRES_USER:-eam_user}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-eam_pass}"
BACKUP_DIR="${BACKUP_DIR:-/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
COMPRESSION_LEVEL="${COMPRESSION_LEVEL:-6}"

# Create backup directory if it doesn't exist
mkdir -p "${BACKUP_DIR}"

# Generate timestamp
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/eam_backup_${TIMESTAMP}.sql.gz"

echo "🗄️ Iniciando backup do PostgreSQL EAM v5.0..."
echo "   Host: ${POSTGRES_HOST}:${POSTGRES_PORT}"
echo "   Database: ${POSTGRES_DB}"
echo "   User: ${POSTGRES_USER}"
echo "   Backup file: ${BACKUP_FILE}"

# Set password for pg_dump
export PGPASSWORD="${POSTGRES_PASSWORD}"

# Create backup with compression
echo "📦 Criando backup completo..."
pg_dump \
  --host="${POSTGRES_HOST}" \
  --port="${POSTGRES_PORT}" \
  --username="${POSTGRES_USER}" \
  --dbname="${POSTGRES_DB}" \
  --no-password \
  --verbose \
  --clean \
  --create \
  --if-exists \
  --format=custom \
  --compress="${COMPRESSION_LEVEL}" \
  --file="${BACKUP_FILE%.gz}" \
  --exclude-table-data='eam.activity_logs_*' \
  --exclude-table-data='eam.mv_*'

# Compress the backup
echo "🗜️ Comprimindo backup..."
gzip -f "${BACKUP_FILE%.gz}"

# Get backup size
BACKUP_SIZE=$(du -h "${BACKUP_FILE}" | cut -f1)
echo "✅ Backup criado com sucesso!"
echo "   Tamanho: ${BACKUP_SIZE}"

# Create schema-only backup for structure
SCHEMA_BACKUP_FILE="${BACKUP_DIR}/eam_schema_${TIMESTAMP}.sql"
echo "📋 Criando backup do schema..."
pg_dump \
  --host="${POSTGRES_HOST}" \
  --port="${POSTGRES_PORT}" \
  --username="${POSTGRES_USER}" \
  --dbname="${POSTGRES_DB}" \
  --no-password \
  --schema-only \
  --clean \
  --create \
  --if-exists \
  --file="${SCHEMA_BACKUP_FILE}"

# Create data-only backup for activity logs (last 7 days)
DATA_BACKUP_FILE="${BACKUP_DIR}/eam_activity_logs_${TIMESTAMP}.sql.gz"
echo "📊 Criando backup dos dados de atividade (últimos 7 dias)..."
pg_dump \
  --host="${POSTGRES_HOST}" \
  --port="${POSTGRES_PORT}" \
  --username="${POSTGRES_USER}" \
  --dbname="${POSTGRES_DB}" \
  --no-password \
  --data-only \
  --table='eam.activity_logs' \
  --where="event_timestamp >= NOW() - INTERVAL '7 days'" \
  --format=custom \
  --compress="${COMPRESSION_LEVEL}" \
  --file="${DATA_BACKUP_FILE%.gz}"

gzip -f "${DATA_BACKUP_FILE%.gz}"

# Clean up old backups
echo "🧹 Limpando backups antigos (${RETENTION_DAYS} dias)..."
find "${BACKUP_DIR}" -name "eam_backup_*.sql.gz" -type f -mtime +${RETENTION_DAYS} -delete
find "${BACKUP_DIR}" -name "eam_schema_*.sql" -type f -mtime +${RETENTION_DAYS} -delete
find "${BACKUP_DIR}" -name "eam_activity_logs_*.sql.gz" -type f -mtime +${RETENTION_DAYS} -delete

# Create backup manifest
MANIFEST_FILE="${BACKUP_DIR}/backup_manifest.json"
cat > "${MANIFEST_FILE}" << EOF
{
  "timestamp": "${TIMESTAMP}",
  "database": "${POSTGRES_DB}",
  "host": "${POSTGRES_HOST}",
  "port": "${POSTGRES_PORT}",
  "user": "${POSTGRES_USER}",
  "backups": {
    "full": {
      "file": "$(basename "${BACKUP_FILE}")",
      "size": "${BACKUP_SIZE}",
      "compression": "${COMPRESSION_LEVEL}",
      "created": "$(date -Iseconds)"
    },
    "schema": {
      "file": "$(basename "${SCHEMA_BACKUP_FILE}")",
      "size": "$(du -h "${SCHEMA_BACKUP_FILE}" | cut -f1)",
      "created": "$(date -Iseconds)"
    },
    "activity_logs": {
      "file": "$(basename "${DATA_BACKUP_FILE}")",
      "size": "$(du -h "${DATA_BACKUP_FILE}" | cut -f1)",
      "days": 7,
      "created": "$(date -Iseconds)"
    }
  },
  "retention_days": ${RETENTION_DAYS}
}
EOF

echo "📄 Manifest criado: ${MANIFEST_FILE}"

# Test backup integrity
echo "🔍 Testando integridade do backup..."
if pg_restore --list "${BACKUP_FILE%.gz}" > /dev/null 2>&1; then
    echo "✅ Backup íntegro!"
else
    echo "❌ Erro na integridade do backup!"
    exit 1
fi

# Create backup report
REPORT_FILE="${BACKUP_DIR}/backup_report_${TIMESTAMP}.txt"
cat > "${REPORT_FILE}" << EOF
=== EAM v5.0 PostgreSQL Backup Report ===
Date: $(date)
Database: ${POSTGRES_DB}
Host: ${POSTGRES_HOST}:${POSTGRES_PORT}
User: ${POSTGRES_USER}

Backup Files:
- Full backup: $(basename "${BACKUP_FILE}") (${BACKUP_SIZE})
- Schema backup: $(basename "${SCHEMA_BACKUP_FILE}") ($(du -h "${SCHEMA_BACKUP_FILE}" | cut -f1))
- Activity logs: $(basename "${DATA_BACKUP_FILE}") ($(du -h "${DATA_BACKUP_FILE}" | cut -f1))

Database Statistics:
- Tables: $(psql -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'eam';" | tr -d ' ')
- Activity logs: $(psql -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -t -c "SELECT COUNT(*) FROM eam.activity_logs;" | tr -d ' ')
- Agents: $(psql -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -t -c "SELECT COUNT(*) FROM eam.agents;" | tr -d ' ')
- Users: $(psql -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -t -c "SELECT COUNT(*) FROM eam.users;" | tr -d ' ')
- Daily scores: $(psql -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -t -c "SELECT COUNT(*) FROM eam.daily_scores;" | tr -d ' ')

Retention: ${RETENTION_DAYS} days
Status: SUCCESS
EOF

echo "📊 Relatório criado: ${REPORT_FILE}"

# List all backups
echo ""
echo "📋 Backups disponíveis:"
ls -lh "${BACKUP_DIR}"/eam_backup_*.sql.gz 2>/dev/null || echo "   Nenhum backup encontrado"

echo ""
echo "✅ Backup concluído com sucesso!"
echo "   Arquivo principal: ${BACKUP_FILE}"
echo "   Tamanho: ${BACKUP_SIZE}"
echo "   Retenção: ${RETENTION_DAYS} dias"

# Unset password
unset PGPASSWORD