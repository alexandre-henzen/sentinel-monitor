#!/bin/bash

# EAM Rollback Script
# Automatic rollback to previous stable version

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
NAMESPACE="eam-system"
TIMEOUT="300s"

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
  -r, --revision Specific revision to rollback to (optional)
  -f, --force    Force rollback without confirmation
  -d, --dry-run  Show what would be rolled back without actually doing it
  -v, --verbose  Enable verbose logging

Examples:
  $0 production
  $0 staging --revision 5
  $0 production --force
EOF
}

# Parse command line arguments
ENVIRONMENT=""
REVISION=""
FORCE=false
DRY_RUN=false
VERBOSE=false

while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            usage
            exit 0
            ;;
        -r|--revision)
            REVISION=$2
            shift 2
            ;;
        -f|--force)
            FORCE=true
            shift
            ;;
        -d|--dry-run)
            DRY_RUN=true
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

# Enable verbose logging if requested
if [[ "${VERBOSE}" == "true" ]]; then
    set -x
fi

log_info "Starting EAM rollback"
log_info "Environment: ${ENVIRONMENT}"
log_info "Namespace: ${NAMESPACE}"

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    # Check if kubectl is installed
    if ! command -v kubectl &> /dev/null; then
        log_error "kubectl is not installed or not in PATH"
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

# Get rollout history
get_rollout_history() {
    log_info "Getting rollout history..."
    
    echo "API Deployment History:"
    kubectl --kubeconfig="${KUBECONFIG}" rollout history deployment/eam-api -n "${NAMESPACE}"
    
    echo ""
    echo "Frontend Deployment History:"
    kubectl --kubeconfig="${KUBECONFIG}" rollout history deployment/eam-frontend -n "${NAMESPACE}"
}

# Confirm rollback
confirm_rollback() {
    if [[ "${FORCE}" == "true" || "${DRY_RUN}" == "true" ]]; then
        return 0
    fi
    
    echo ""
    log_warning "This will rollback the EAM deployment in ${ENVIRONMENT} environment"
    log_warning "This action cannot be undone!"
    echo ""
    
    read -p "Are you sure you want to proceed? (yes/no): " -r
    if [[ ! $REPLY =~ ^[Yy][Ee][Ss]$ ]]; then
        log_info "Rollback cancelled by user"
        exit 0
    fi
}

# Perform rollback
perform_rollback() {
    log_info "Performing rollback..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would rollback deployments"
        if [[ -n "${REVISION}" ]]; then
            log_info "[DRY RUN] Would rollback to revision: ${REVISION}"
        else
            log_info "[DRY RUN] Would rollback to previous revision"
        fi
        return 0
    fi
    
    # Create rollback timestamp
    local rollback_timestamp=$(date +%Y%m%d-%H%M%S)
    
    # Rollback API deployment
    log_info "Rolling back API deployment..."
    if [[ -n "${REVISION}" ]]; then
        kubectl --kubeconfig="${KUBECONFIG}" rollout undo deployment/eam-api \
            --to-revision="${REVISION}" -n "${NAMESPACE}"
    else
        kubectl --kubeconfig="${KUBECONFIG}" rollout undo deployment/eam-api -n "${NAMESPACE}"
    fi
    
    # Rollback Frontend deployment
    log_info "Rolling back Frontend deployment..."
    if [[ -n "${REVISION}" ]]; then
        kubectl --kubeconfig="${KUBECONFIG}" rollout undo deployment/eam-frontend \
            --to-revision="${REVISION}" -n "${NAMESPACE}"
    else
        kubectl --kubeconfig="${KUBECONFIG}" rollout undo deployment/eam-frontend -n "${NAMESPACE}"
    fi
    
    # Add rollback annotation
    kubectl --kubeconfig="${KUBECONFIG}" annotate deployment/eam-api \
        deployment.kubernetes.io/rollback-timestamp="${rollback_timestamp}" \
        -n "${NAMESPACE}" --overwrite
    
    kubectl --kubeconfig="${KUBECONFIG}" annotate deployment/eam-frontend \
        deployment.kubernetes.io/rollback-timestamp="${rollback_timestamp}" \
        -n "${NAMESPACE}" --overwrite
    
    log_success "Rollback initiated"
}

# Wait for rollback to complete
wait_for_rollback() {
    log_info "Waiting for rollback to complete..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would wait for rollback completion"
        return 0
    fi
    
    # Wait for API deployment rollback
    log_info "Waiting for API deployment rollback..."
    if ! kubectl --kubeconfig="${KUBECONFIG}" rollout status deployment/eam-api \
        -n "${NAMESPACE}" --timeout="${TIMEOUT}"; then
        log_error "API deployment rollback failed"
        return 1
    fi
    
    # Wait for Frontend deployment rollback
    log_info "Waiting for Frontend deployment rollback..."
    if ! kubectl --kubeconfig="${KUBECONFIG}" rollout status deployment/eam-frontend \
        -n "${NAMESPACE}" --timeout="${TIMEOUT}"; then
        log_error "Frontend deployment rollback failed"
        return 1
    fi
    
    log_success "Rollback completed successfully"
}

# Perform health checks after rollback
post_rollback_health_check() {
    log_info "Performing post-rollback health checks..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would perform health checks"
        return 0
    fi
    
    # Use the health check script
    if [[ -f "${SCRIPT_DIR}/health-check.sh" ]]; then
        "${SCRIPT_DIR}/health-check.sh" "${ENVIRONMENT}"
    else
        log_warning "Health check script not found, performing basic checks..."
        
        # Check pod status
        local unhealthy_pods=$(kubectl --kubeconfig="${KUBECONFIG}" get pods -n "${NAMESPACE}" \
            --field-selector=status.phase!=Running -o name | wc -l)
        
        if [[ ${unhealthy_pods} -gt 0 ]]; then
            log_error "Found ${unhealthy_pods} unhealthy pods after rollback"
            kubectl --kubeconfig="${KUBECONFIG}" get pods -n "${NAMESPACE}" \
                --field-selector=status.phase!=Running
            return 1
        fi
        
        log_success "Basic health checks passed"
    fi
}

# Generate rollback report
generate_rollback_report() {
    log_info "Generating rollback report..."
    
    local report_file="/tmp/eam-rollback-report-$(date +%Y%m%d-%H%M%S).txt"
    
    cat > "${report_file}" << EOF
EAM Rollback Report
==================

Environment: ${ENVIRONMENT}
Namespace: ${NAMESPACE}
Timestamp: $(date)
Initiated by: $(whoami)

Rollback Details:
EOF
    
    if [[ -n "${REVISION}" ]]; then
        echo "- Rolled back to revision: ${REVISION}" >> "${report_file}"
    else
        echo "- Rolled back to previous revision" >> "${report_file}"
    fi
    
    echo "" >> "${report_file}"
    echo "Current Deployment Status:" >> "${report_file}"
    
    if [[ "${DRY_RUN}" != "true" ]]; then
        kubectl --kubeconfig="${KUBECONFIG}" get deployments -n "${NAMESPACE}" -o wide >> "${report_file}"
        echo "" >> "${report_file}"
        kubectl --kubeconfig="${KUBECONFIG}" get pods -n "${NAMESPACE}" -o wide >> "${report_file}"
    else
        echo "[DRY RUN] Status not available" >> "${report_file}"
    fi
    
    log_success "Rollback report saved to: ${report_file}"
}

# Send notifications
send_notifications() {
    log_info "Sending rollback notifications..."
    
    if [[ "${DRY_RUN}" == "true" ]]; then
        log_info "[DRY RUN] Would send notifications"
        return 0
    fi
    
    # Send Slack notification if webhook is configured
    if [[ -n "${SLACK_WEBHOOK:-}" ]]; then
        local status_icon="⚠️"
        local status_color="warning"
        
        if [[ $? -eq 0 ]]; then
            status_icon="✅"
            status_color="good"
        else
            status_icon="❌"
            status_color="danger"
        fi
        
        local message=$(cat << EOF
{
    "attachments": [
        {
            "color": "${status_color}",
            "title": "${status_icon} EAM Rollback ${ENVIRONMENT^}",
            "fields": [
                {
                    "title": "Environment",
                    "value": "${ENVIRONMENT}",
                    "short": true
                },
                {
                    "title": "Namespace",
                    "value": "${NAMESPACE}",
                    "short": true
                },
                {
                    "title": "Timestamp",
                    "value": "$(date)",
                    "short": false
                }
            ]
        }
    ]
}
EOF
        )
        
        curl -X POST -H 'Content-type: application/json' \
            --data "${message}" \
            "${SLACK_WEBHOOK}" || log_warning "Failed to send Slack notification"
    fi
    
    # Send email notification if configured
    if [[ -n "${EMAIL_NOTIFICATION:-}" ]]; then
        echo "EAM rollback completed for ${ENVIRONMENT} environment at $(date)" | \
            mail -s "EAM Rollback ${ENVIRONMENT^}" "${EMAIL_NOTIFICATION}" || \
            log_warning "Failed to send email notification"
    fi
}

# Main rollback function
main() {
    # Check prerequisites
    check_prerequisites
    
    # Show rollout history
    get_rollout_history
    
    # Confirm rollback
    confirm_rollback
    
    # Perform rollback
    perform_rollback
    
    # Wait for rollback to complete
    if ! wait_for_rollback; then
        log_error "Rollback failed"
        exit 1
    fi
    
    # Perform health checks
    if ! post_rollback_health_check; then
        log_error "Post-rollback health checks failed"
        exit 1
    fi
    
    # Generate report
    generate_rollback_report
    
    # Send notifications
    send_notifications
    
    log_success "Rollback completed successfully!"
    log_info "Environment: ${ENVIRONMENT}"
    log_info "Namespace: ${NAMESPACE}"
    
    # Show final status
    if [[ "${DRY_RUN}" != "true" ]]; then
        echo ""
        log_info "Current deployment status:"
        kubectl --kubeconfig="${KUBECONFIG}" get deployments -n "${NAMESPACE}" -o wide
        echo ""
        kubectl --kubeconfig="${KUBECONFIG}" get pods -n "${NAMESPACE}" -o wide
    fi
}

# Run main function
main "$@"