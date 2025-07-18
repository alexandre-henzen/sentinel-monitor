#!/bin/bash

# EAM Backup Script
# Automated backup of database and critical configurations

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NAMESPACE="eam-system"
BACKUP_DIR="/var/backups/eam"
RETENTION_DAYS=30
TIMESTAMP=$(date +%Y%m%d-%H%M%S)

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Usage function
usage() {
    cat << EOF
Usage: $0 <environment> [options]

Arguments:
  environment    Target environment (staging|production)

Options:
  -h, --help     Show this help message
  -d, --dir      Backup directory (default: ${BACKUP_DIR})
  -r, --retention Retention days (default: ${RETENTION_DAYS})
  -t, --type     Backup type (full|database|config|all) (default: all)
  -c, --compress Compress backup files
  -v, --verbose  Enable verbose logging
  -n, --dry-run  Show what would be backed up without actually doing it

Examples:
  $0 production
  $0 staging --type database
  $0 production --compress --retention 90
EOF
}

# Parse command line arguments
ENVIRONMENT=""
BACKUP_TYPE="all"
COMPRESS=false
VERBOSE=false
DRY_RUN=false

while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            usage
            exit 0
            ;;
        -d|--dir)
            BACKUP_DIR=$2
            shift 2
            ;;
        -r|--retention)
            RETENTION_DAYS=$2
            shift 2
            ;;
        -t|--type)
            BACKUP_TYPE=$2
            shift 2
            ;;
        -c|--compress)
            COMPRESS=true
            shift
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -n|--dry-run)
            DRY_RUN=true
            shift
            ;;
        staging|production)
            ENVIRONMENT=$1
            shift
            ;;
        *)
            log_error "Unknown option: $1"
            usage
            exit 1
            ;;
    esac
done

# Validate environment
if [[ -z "${ENVIRONMENT}" ]]; then
    log_error "Environment is required"
    usage
    exit 1
fi

if [[ "${ENVIRONMENT}" != "staging" && "${ENVIRONMENT}" != "production" ]]; then
    log_error "Invalid environment: ${ENVIRONMENT}. Must be 'staging' or 'production'"
    exit 1
fi

# Set environment-specific variables
case "${ENVIRONMENT}" in
    staging)
        NAMESPACE="eam-staging"
        KUBECONFIG="${HOME}/.kube/config-staging"
        ;;
    production)
        NAMESPACE="eam-production"
        KUBECONFIG="${HOME}/.kube/config-production"
        ;;
esac

# Create backup directory structure
BACKUP_PATH="${BACKUP_DIR}/${ENVIRONMENT}/${TIMESTAMP}"
DATABASE_BACKUP_PATH="${BACKUP_PATH}/database"
CONFIG_BACKUP_PATH="${BACKUP_PATH}/config"
MANIFEST_BACKUP_PATH="${BACKUP_PATH}/manifests"

# Enable verbose logging if requested
if [[ "${VERBOSE}" == "true" ]]; then
    set -x
fi

log_info "Starting EAM backup"
log_info "Environment: ${ENVIRONMENT}"
log_info "Backup type: ${BACKUP_TYPE}"
log_info "Backup path: ${BACKUP_PATH}"

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    # Check if kubectl is installed
    if ! command -v kubectl &> /dev/null; then
        log_error "kubectl is not installed or not in PATH"
        exit 1
    fi
    
    # Check if pg_dump is installed (for database backups)
    if [[ "${BACKUP_TYPE}" == "all" || "${BACKUP_TYPE}" == "database" || "${BACKUP_TYPE}" == "full" ]]; then
        if ! command -v pg_dump &> /dev/null; then
            log_error "pg_dump is not installed or not in PATH"
            exit 1
        fi
    fi
    
    # Check if kubeconfig exists
    if [[ ! -f "${KUBECONFIG}" ]]; then
        log_error "Kubeconfig file not found: ${KUBECONFIG}"
        exit 1
    fi
    
    # Test cluster connectivity
    if ! kubectl --kubeconfig="${KUBECONFIG}" cluster-info &> /dev/null; then
        log_error "Cannot connect to Kubernetes cluster"
        exit 1
    fi
    
    log_success "Prerequisites check passed"
}

# Create backup directories
create_backup_dirs() {
    log_info "Creating backup directories..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would create directories: ${BACKUP_PATH}"
        return 0
    fi
    
    mkdir -p "${DATABASE_BACKUP_PATH}"
    mkdir -p "${CONFIG_BACKUP_PATH}"
    mkdir -p "${MANIFEST_BACKUP_PATH}"
    
    log_success "Backup directories created"
}

# Backup database
backup_database() {
    if [[ "${BACKUP_TYPE}" != "all" && "${BACKUP_TYPE}" != "database" && "${BACKUP_TYPE}" != "full" ]]; then
        return 0
    fi
    
    log_info "Backing up database..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would backup database"
        return 0
    fi
    
    # Get database credentials from secrets
    local db_host=$(kubectl --kubeconfig="${KUBECONFIG}" get secret eam-secrets \
        -n "${NAMESPACE}" -o jsonpath='{.data.database-host}' | base64 -d 2>/dev/null || echo "postgresql-service")
    local db_port=$(kubectl --kubeconfig="${KUBECONFIG}" get secret eam-secrets \
        -n "${NAMESPACE}" -o jsonpath='{.data.database-port}' | base64 -d 2>/dev/null || echo "5432")
    local db_name=$(kubectl --kubeconfig="${KUBECONFIG}" get secret eam-secrets \
        -n "${NAMESPACE}" -o jsonpath='{.data.database-name}' | base64 -d 2>/dev/null || echo "eam")
    local db_user=$(kubectl --kubeconfig="${KUBECONFIG}" get secret eam-secrets \
        -n "${NAMESPACE}" -o jsonpath='{.data.database-user}' | base64 -d 2>/dev/null || echo "eam_user")
    local db_password=$(kubectl --kubeconfig="${KUBECONFIG}" get secret eam-secrets \
        -n "${NAMESPACE}" -o jsonpath='{.data.database-password}' | base64 -d 2>/dev/null || echo "")
    
    if [[ -z "${db_password}" ]]; then
        log_error "Database password not found in secrets"
        return 1
    fi
    
    # Port forward to database
    local port_forward_pid=""
    local local_port=5433
    
    log_info "Creating port forward to database..."
    kubectl --kubeconfig="${KUBECONFIG}" port-forward svc/postgresql-service ${local_port}:5432 \
        -n "${NAMESPACE}" &> /dev/null &
    port_forward_pid=$!
    
    # Wait for port forward to be ready
    sleep 5
    
    # Backup database
    local backup_file="${DATABASE_BACKUP_PATH}/eam-database-${TIMESTAMP}.sql"
    
    PGPASSWORD="${db_password}" pg_dump \
        --host=localhost \
        --port=${local_port} \
        --username="${db_user}" \
        --dbname="${db_name}" \
        --verbose \
        --clean \
        --if-exists \
        --create \
        --format=custom \
        --file="${backup_file}" || {
        log_error "Database backup failed"
        kill ${port_forward_pid} 2>/dev/null || true
        return 1
    }
    
    # Kill port forward
    kill ${port_forward_pid} 2>/dev/null || true
    
    # Compress if requested
    if [[ "${COMPRESS}" == "true" ]]; then
        log_info "Compressing database backup..."
        gzip "${backup_file}"
        backup_file="${backup_file}.gz"
    fi
    
    # Verify backup
    if [[ -f "${backup_file}" ]]; then
        local backup_size=$(du -h "${backup_file}" | cut -f1)
        log_success "Database backup completed: ${backup_file} (${backup_size})"
    else
        log_error "Database backup file not found"
        return 1
    fi
}

# Backup configurations
backup_configurations() {
    if [[ "${BACKUP_TYPE}" != "all" && "${BACKUP_TYPE}" != "config" && "${BACKUP_TYPE}" != "full" ]]; then
        return 0
    fi
    
    log_info "Backing up configurations..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would backup configurations"
        return 0
    fi
    
    # Backup ConfigMaps
    kubectl --kubeconfig="${KUBECONFIG}" get configmaps -n "${NAMESPACE}" -o yaml \
        > "${CONFIG_BACKUP_PATH}/configmaps-${TIMESTAMP}.yaml"
    
    # Backup Secrets (without sensitive data)
    kubectl --kubeconfig="${KUBECONFIG}" get secrets -n "${NAMESPACE}" -o yaml \
        | sed 's/data:/data: {}/g' \
        > "${CONFIG_BACKUP_PATH}/secrets-structure-${TIMESTAMP}.yaml"
    
    # Backup PersistentVolumeClaims
    kubectl --kubeconfig="${KUBECONFIG}" get pvc -n "${NAMESPACE}" -o yaml \
        > "${CONFIG_BACKUP_PATH}/pvc-${TIMESTAMP}.yaml" 2>/dev/null || true
    
    # Backup ServiceAccounts
    kubectl --kubeconfig="${KUBECONFIG}" get serviceaccounts -n "${NAMESPACE}" -o yaml \
        > "${CONFIG_BACKUP_PATH}/serviceaccounts-${TIMESTAMP}.yaml"
    
    # Backup RBAC
    kubectl --kubeconfig="${KUBECONFIG}" get roles,rolebindings -n "${NAMESPACE}" -o yaml \
        > "${CONFIG_BACKUP_PATH}/rbac-${TIMESTAMP}.yaml"
    
    log_success "Configurations backup completed"
}

# Backup Kubernetes manifests
backup_manifests() {
    if [[ "${BACKUP_TYPE}" != "all" && "${BACKUP_TYPE}" != "full" ]]; then
        return 0
    fi
    
    log_info "Backing up Kubernetes manifests..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would backup manifests"
        return 0
    fi
    
    # Backup Deployments
    kubectl --kubeconfig="${KUBECONFIG}" get deployments -n "${NAMESPACE}" -o yaml \
        > "${MANIFEST_BACKUP_PATH}/deployments-${TIMESTAMP}.yaml"
    
    # Backup Services
    kubectl --kubeconfig="${KUBECONFIG}" get services -n "${NAMESPACE}" -o yaml \
        > "${MANIFEST_BACKUP_PATH}/services-${TIMESTAMP}.yaml"
    
    # Backup Ingress
    kubectl --kubeconfig="${KUBECONFIG}" get ingress -n "${NAMESPACE}" -o yaml \
        > "${MANIFEST_BACKUP_PATH}/ingress-${TIMESTAMP}.yaml" 2>/dev/null || true
    
    # Backup HPA
    kubectl --kubeconfig="${KUBECONFIG}" get hpa -n "${NAMESPACE}" -o yaml \
        > "${MANIFEST_BACKUP_PATH}/hpa-${TIMESTAMP}.yaml" 2>/dev/null || true
    
    # Backup PodDisruptionBudgets
    kubectl --kubeconfig="${KUBECONFIG}" get pdb -n "${NAMESPACE}" -o yaml \
        > "${MANIFEST_BACKUP_PATH}/pdb-${TIMESTAMP}.yaml" 2>/dev/null || true
    
    # Backup NetworkPolicies
    kubectl --kubeconfig="${KUBECONFIG}" get networkpolicies -n "${NAMESPACE}" -o yaml \
        > "${MANIFEST_BACKUP_PATH}/networkpolicies-${TIMESTAMP}.yaml" 2>/dev/null || true
    
    log_success "Kubernetes manifests backup completed"
}

# Create backup metadata
create_backup_metadata() {
    log_info "Creating backup metadata..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would create metadata"
        return 0
    fi
    
    cat > "${BACKUP_PATH}/metadata.json" << EOF
{
    "timestamp": "${TIMESTAMP}",
    "environment": "${ENVIRONMENT}",
    "namespace": "${NAMESPACE}",
    "backup_type": "${BACKUP_TYPE}",
    "compressed": ${COMPRESS},
    "retention_days": ${RETENTION_DAYS},
    "created_by": "$(whoami)",
    "hostname": "$(hostname)",
    "kubernetes_version": "$(kubectl --kubeconfig="${KUBECONFIG}" version --short --client | grep Client | cut -d' ' -f3)",
    "cluster_info": {
        "server": "$(kubectl --kubeconfig="${KUBECONFIG}" cluster-info | grep 'Kubernetes control plane' | cut -d' ' -f6)",
        "context": "$(kubectl --kubeconfig="${KUBECONFIG}" config current-context)"
    }
}
EOF
    
    log_success "Backup metadata created"
}

# Compress backup if requested
compress_backup() {
    if [[ "${COMPRESS}" != "true" ]]; then
        return 0
    fi
    
    log_info "Compressing backup..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would compress backup"
        return 0
    fi
    
    local archive_name="eam-backup-${ENVIRONMENT}-${TIMESTAMP}.tar.gz"
    local archive_path="${BACKUP_DIR}/${ENVIRONMENT}/${archive_name}"
    
    tar -czf "${archive_path}" -C "${BACKUP_DIR}/${ENVIRONMENT}" "${TIMESTAMP}"
    
    if [[ -f "${archive_path}" ]]; then
        local archive_size=$(du -h "${archive_path}" | cut -f1)
        log_success "Backup compressed: ${archive_path} (${archive_size})"
        
        # Remove uncompressed backup
        rm -rf "${BACKUP_PATH}"
    else
        log_error "Backup compression failed"
        return 1
    fi
}

# Clean old backups
cleanup_old_backups() {
    log_info "Cleaning up old backups..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would cleanup backups older than ${RETENTION_DAYS} days"
        return 0
    fi
    
    local cleanup_count=0
    
    # Find and remove old backup directories
    while IFS= read -r -d '' backup_dir; do
        if [[ -d "${backup_dir}" ]]; then
            rm -rf "${backup_dir}"
            cleanup_count=$((cleanup_count + 1))
        fi
    done < <(find "${BACKUP_DIR}/${ENVIRONMENT}" -maxdepth 1 -type d -mtime +${RETENTION_DAYS} -print0 2>/dev/null)
    
    # Find and remove old backup archives
    while IFS= read -r -d '' backup_file; do
        if [[ -f "${backup_file}" ]]; then
            rm -f "${backup_file}"
            cleanup_count=$((cleanup_count + 1))
        fi
    done < <(find "${BACKUP_DIR}/${ENVIRONMENT}" -maxdepth 1 -name "*.tar.gz" -mtime +${RETENTION_DAYS} -print0 2>/dev/null)
    
    if [[ ${cleanup_count} -gt 0 ]]; then
        log_success "Cleaned up ${cleanup_count} old backups"
    else
        log_info "No old backups to clean up"
    fi
}

# Generate backup report
generate_backup_report() {
    log_info "Generating backup report..."
    
    local report_file="${BACKUP_PATH}/backup-report.txt"
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would generate backup report"
        return 0
    fi
    
    cat > "${report_file}" << EOF
EAM Backup Report
=================

Environment: ${ENVIRONMENT}
Namespace: ${NAMESPACE}
Timestamp: ${TIMESTAMP}
Backup Type: ${BACKUP_TYPE}
Compressed: ${COMPRESS}
Created By: $(whoami)
Hostname: $(hostname)

Backup Contents:
EOF
    
    if [[ -d "${DATABASE_BACKUP_PATH}" ]]; then
        echo "- Database backup: $(ls -la "${DATABASE_BACKUP_PATH}" | wc -l) files" >> "${report_file}"
    fi
    
    if [[ -d "${CONFIG_BACKUP_PATH}" ]]; then
        echo "- Configuration backup: $(ls -la "${CONFIG_BACKUP_PATH}" | wc -l) files" >> "${report_file}"
    fi
    
    if [[ -d "${MANIFEST_BACKUP_PATH}" ]]; then
        echo "- Manifest backup: $(ls -la "${MANIFEST_BACKUP_PATH}" | wc -l) files" >> "${report_file}"
    fi
    
    echo "" >> "${report_file}"
    echo "Total Backup Size: $(du -sh "${BACKUP_PATH}" | cut -f1)" >> "${report_file}"
    
    log_success "Backup report generated: ${report_file}"
}

# Main backup function
main() {
    # Check prerequisites
    check_prerequisites
    
    # Create backup directories
    create_backup_dirs
    
    # Perform backups based on type
    case "${BACKUP_TYPE}" in
        "database")
            backup_database
            ;;
        "config")
            backup_configurations
            ;;
        "all"|"full")
            backup_database
            backup_configurations
            backup_manifests
            ;;
        *)
            log_error "Invalid backup type: ${BACKUP_TYPE}"
            exit 1
            ;;
    esac
    
    # Create metadata and report
    create_backup_metadata
    generate_backup_report
    
    # Compress if requested
    compress_backup
    
    # Clean old backups
    cleanup_old_backups
    
    log_success "Backup completed successfully!"
    log_info "Environment: ${ENVIRONMENT}"
    log_info "Backup path: ${BACKUP_PATH}"
    log_info "Backup type: ${BACKUP_TYPE}"
    
    if [[ "${DRY_RUN}" != "true" ]]; then
        log_info "Total backup size: $(du -sh "${BACKUP_PATH}" | cut -f1)"
    fi
}

# Run main function
main "$@"