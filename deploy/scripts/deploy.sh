#!/bin/bash

# EAM Deployment Script
# Zero-downtime deployment with automatic rollback on failure

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
DEPLOY_DIR="${PROJECT_ROOT}/deploy"
NAMESPACE="eam-system"
TIMEOUT="600s"
HEALTH_CHECK_RETRIES=10
HEALTH_CHECK_INTERVAL=30

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
Usage: $0 <environment> [version]

Arguments:
  environment    Target environment (staging|production)
  version        Version to deploy (optional, defaults to latest)

Options:
  -h, --help     Show this help message
  -d, --dry-run  Show what would be deployed without actually deploying
  -f, --force    Force deployment even if health checks fail
  -s, --skip-backup Skip database backup (not recommended for production)
  -v, --verbose  Enable verbose logging

Examples:
  $0 staging
  $0 production 5.0.1
  $0 production --dry-run
EOF
}

# Parse command line arguments
ENVIRONMENT=""
VERSION="latest"
DRY_RUN=false
FORCE=false
SKIP_BACKUP=false
VERBOSE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            usage
            exit 0
            ;;
        -d|--dry-run)
            DRY_RUN=true
            shift
            ;;
        -f|--force)
            FORCE=true
            shift
            ;;
        -s|--skip-backup)
            SKIP_BACKUP=true
            shift
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        staging|production)
            ENVIRONMENT=$1
            shift
            ;;
        *)
            if [[ -z "${ENVIRONMENT}" ]]; then
                ENVIRONMENT=$1
            else
                VERSION=$1
            fi
            shift
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

# Enable verbose logging if requested
if [[ "${VERBOSE}" == "true" ]]; then
    set -x
fi

log_info "Starting EAM deployment"
log_info "Environment: ${ENVIRONMENT}"
log_info "Version: ${VERSION}"
log_info "Namespace: ${NAMESPACE}"

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    # Check if kubectl is installed
    if ! command -v kubectl &> /dev/null; then
        log_error "kubectl is not installed or not in PATH"
        exit 1
    fi
    
    # Check if helm is installed
    if ! command -v helm &> /dev/null; then
        log_error "helm is not installed or not in PATH"
        exit 1
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

# Create namespace if it doesn't exist
create_namespace() {
    log_info "Creating namespace if needed..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would create namespace: ${NAMESPACE}"
        return
    fi
    
    kubectl --kubeconfig="${KUBECONFIG}" apply -f "${DEPLOY_DIR}/kubernetes/namespace.yml"
    log_success "Namespace created/updated"
}

# Deploy secrets (if they don't exist)
deploy_secrets() {
    log_info "Deploying secrets..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would deploy secrets"
        return
    fi
    
    # Check if secrets exist
    if kubectl --kubeconfig="${KUBECONFIG}" get secret eam-secrets -n "${NAMESPACE}" &> /dev/null; then
        log_info "Secrets already exist, skipping creation"
    else
        log_warning "Secrets not found. Please create them manually or use the secrets management script"
        log_warning "Required secrets: eam-secrets"
        if [[ "${FORCE}" != "true" ]]; then
            exit 1
        fi
    fi
}

# Deploy configuration
deploy_config() {
    log_info "Deploying configuration..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would deploy configuration"
        return
    fi
    
    kubectl --kubeconfig="${KUBECONFIG}" apply -f "${DEPLOY_DIR}/kubernetes/configmaps/"
    log_success "Configuration deployed"
}

# Deploy infrastructure services
deploy_infrastructure() {
    log_info "Deploying infrastructure services..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would deploy infrastructure"
        return
    fi
    
    # Deploy PostgreSQL, Redis, MinIO, monitoring, etc.
    if [[ -d "${DEPLOY_DIR}/kubernetes/infrastructure" ]]; then
        kubectl --kubeconfig="${KUBECONFIG}" apply -f "${DEPLOY_DIR}/kubernetes/infrastructure/"
        log_success "Infrastructure services deployed"
    else
        log_warning "Infrastructure manifests not found, skipping"
    fi
}

# Deploy application
deploy_application() {
    log_info "Deploying application..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would deploy application with version: ${VERSION}"
        return
    fi
    
    # Update image tags if version is specified
    if [[ "${VERSION}" != "latest" ]]; then
        log_info "Updating image tags to version: ${VERSION}"
        
        # Update API deployment
        kubectl --kubeconfig="${KUBECONFIG}" set image deployment/eam-api \
            eam-api="ghcr.io/company/eam-api:${VERSION}" \
            -n "${NAMESPACE}"
        
        # Update Frontend deployment
        kubectl --kubeconfig="${KUBECONFIG}" set image deployment/eam-frontend \
            eam-frontend="ghcr.io/company/eam-frontend:${VERSION}" \
            -n "${NAMESPACE}"
    fi
    
    # Deploy services
    kubectl --kubeconfig="${KUBECONFIG}" apply -f "${DEPLOY_DIR}/kubernetes/services/"
    
    # Deploy deployments
    kubectl --kubeconfig="${KUBECONFIG}" apply -f "${DEPLOY_DIR}/kubernetes/deployments/"
    
    # Deploy ingress
    kubectl --kubeconfig="${KUBECONFIG}" apply -f "${DEPLOY_DIR}/kubernetes/ingress/"
    
    log_success "Application deployed"
}

# Wait for rollout to complete
wait_for_rollout() {
    log_info "Waiting for rollout to complete..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would wait for rollout"
        return
    fi
    
    # Wait for API deployment
    if ! kubectl --kubeconfig="${KUBECONFIG}" rollout status deployment/eam-api \
        -n "${NAMESPACE}" --timeout="${TIMEOUT}"; then
        log_error "API deployment rollout failed"
        return 1
    fi
    
    # Wait for Frontend deployment
    if ! kubectl --kubeconfig="${KUBECONFIG}" rollout status deployment/eam-frontend \
        -n "${NAMESPACE}" --timeout="${TIMEOUT}"; then
        log_error "Frontend deployment rollout failed"
        return 1
    fi
    
    log_success "Rollout completed successfully"
}

# Perform health checks
health_check() {
    log_info "Performing health checks..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would perform health checks"
        return
    fi
    
    local retries=0
    local max_retries="${HEALTH_CHECK_RETRIES}"
    
    while [[ ${retries} -lt ${max_retries} ]]; do
        log_info "Health check attempt $((retries + 1))/${max_retries}"
        
        # Check API health
        if kubectl --kubeconfig="${KUBECONFIG}" get pods -n "${NAMESPACE}" \
            -l app=eam-api --field-selector=status.phase=Running | grep -q "Running"; then
            log_success "API health check passed"
            
            # Check Frontend health
            if kubectl --kubeconfig="${KUBECONFIG}" get pods -n "${NAMESPACE}" \
                -l app=eam-frontend --field-selector=status.phase=Running | grep -q "Running"; then
                log_success "Frontend health check passed"
                log_success "All health checks passed"
                return 0
            fi
        fi
        
        retries=$((retries + 1))
        if [[ ${retries} -lt ${max_retries} ]]; then
            log_warning "Health check failed, retrying in ${HEALTH_CHECK_INTERVAL}s..."
            sleep "${HEALTH_CHECK_INTERVAL}"
        fi
    done
    
    log_error "Health checks failed after ${max_retries} attempts"
    return 1
}

# Rollback on failure
rollback_on_failure() {
    log_error "Deployment failed, initiating rollback..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would rollback deployment"
        return
    fi
    
    # Call rollback script
    "${SCRIPT_DIR}/rollback.sh" "${ENVIRONMENT}"
}

# Main deployment function
main() {
    # Set trap for cleanup on failure
    trap 'rollback_on_failure' ERR
    
    # Check prerequisites
    check_prerequisites
    
    # Create backup if not skipped and in production
    if [[ "${SKIP_BACKUP}" != "true" && "${ENVIRONMENT}" == "production" ]]; then
        log_info "Creating backup..."
        "${SCRIPT_DIR}/backup.sh" "${ENVIRONMENT}"
    fi
    
    # Deploy components
    create_namespace
    deploy_secrets
    deploy_config
    deploy_infrastructure
    deploy_application
    
    # Wait for rollout and perform health checks
    if ! wait_for_rollout; then
        log_error "Rollout failed"
        exit 1
    fi
    
    if ! health_check; then
        if [[ "${FORCE}" != "true" ]]; then
            log_error "Health checks failed"
            exit 1
        else
            log_warning "Health checks failed but --force was specified, continuing..."
        fi
    fi
    
    log_success "Deployment completed successfully!"
    log_info "Environment: ${ENVIRONMENT}"
    log_info "Version: ${VERSION}"
    log_info "Namespace: ${NAMESPACE}"
    
    # Show deployment status
    kubectl --kubeconfig="${KUBECONFIG}" get pods -n "${NAMESPACE}" -o wide
}

# Run main function
main "$@"