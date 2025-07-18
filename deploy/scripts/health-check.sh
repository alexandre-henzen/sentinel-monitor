#!/bin/bash

# EAM Health Check Script
# Comprehensive health checks for all EAM components

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NAMESPACE="eam-system"
TIMEOUT=30
MAX_RETRIES=5
RETRY_INTERVAL=10

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
  -v, --verbose  Enable verbose logging
  -q, --quiet    Suppress non-error output
  -t, --timeout  Timeout for individual checks (default: ${TIMEOUT}s)
  -r, --retries  Maximum number of retries (default: ${MAX_RETRIES})
  -c, --component Check specific component (api|frontend|all)
  -o, --output   Output format (text|json|prometheus)

Examples:
  $0 production
  $0 staging --component api
  $0 production --output json
EOF
}

# Parse command line arguments
ENVIRONMENT=""
VERBOSE=false
QUIET=false
COMPONENT="all"
OUTPUT_FORMAT="text"

while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            usage
            exit 0
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -q|--quiet)
            QUIET=true
            shift
            ;;
        -t|--timeout)
            TIMEOUT=$2
            shift 2
            ;;
        -r|--retries)
            MAX_RETRIES=$2
            shift 2
            ;;
        -c|--component)
            COMPONENT=$2
            shift 2
            ;;
        -o|--output)
            OUTPUT_FORMAT=$2
            shift 2
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
        API_URL="https://api-staging.eam.company.com"
        FRONTEND_URL="https://staging.eam.company.com"
        ;;
    production)
        NAMESPACE="eam-production"
        KUBECONFIG="${HOME}/.kube/config-production"
        API_URL="https://api.eam.company.com"
        FRONTEND_URL="https://eam.company.com"
        ;;
esac

# Enable verbose logging if requested
if [[ "${VERBOSE}" == "true" ]]; then
    set -x
fi

# Quiet mode
if [[ "${QUIET}" == "true" ]]; then
    exec 1>/dev/null
fi

# Health check results
declare -A HEALTH_RESULTS
OVERALL_STATUS="healthy"

# Log function that respects quiet mode
log_check() {
    if [[ "${QUIET}" != "true" ]]; then
        log_info "$1"
    fi
}

# Check prerequisites
check_prerequisites() {
    log_check "Checking prerequisites..."
    
    # Check if kubectl is installed
    if ! command -v kubectl &> /dev/null; then
        log_error "kubectl is not installed or not in PATH"
        exit 1
    fi
    
    # Check if curl is installed
    if ! command -v curl &> /dev/null; then
        log_error "curl is not installed or not in PATH"
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
    
    log_check "Prerequisites check passed"
}

# Check Kubernetes resources
check_kubernetes_resources() {
    log_check "Checking Kubernetes resources..."
    
    local status="healthy"
    
    # Check namespace
    if ! kubectl --kubeconfig="${KUBECONFIG}" get namespace "${NAMESPACE}" &> /dev/null; then
        log_error "Namespace ${NAMESPACE} not found"
        status="unhealthy"
    fi
    
    # Check deployments
    local deployments=("eam-api" "eam-frontend")
    for deployment in "${deployments[@]}"; do
        if [[ "${COMPONENT}" == "all" || "${COMPONENT}" == "${deployment#eam-}" ]]; then
            local ready_replicas=$(kubectl --kubeconfig="${KUBECONFIG}" get deployment "${deployment}" \
                -n "${NAMESPACE}" -o jsonpath='{.status.readyReplicas}' 2>/dev/null || echo "0")
            local desired_replicas=$(kubectl --kubeconfig="${KUBECONFIG}" get deployment "${deployment}" \
                -n "${NAMESPACE}" -o jsonpath='{.spec.replicas}' 2>/dev/null || echo "0")
            
            if [[ "${ready_replicas}" != "${desired_replicas}" ]]; then
                log_error "Deployment ${deployment}: ${ready_replicas}/${desired_replicas} replicas ready"
                status="unhealthy"
            else
                log_check "Deployment ${deployment}: ${ready_replicas}/${desired_replicas} replicas ready"
            fi
        fi
    done
    
    # Check pods
    local unhealthy_pods=$(kubectl --kubeconfig="${KUBECONFIG}" get pods -n "${NAMESPACE}" \
        --field-selector=status.phase!=Running -o name 2>/dev/null | wc -l)
    
    if [[ ${unhealthy_pods} -gt 0 ]]; then
        log_error "Found ${unhealthy_pods} unhealthy pods"
        status="unhealthy"
    else
        log_check "All pods are running"
    fi
    
    HEALTH_RESULTS["kubernetes"]="${status}"
    if [[ "${status}" == "unhealthy" ]]; then
        OVERALL_STATUS="unhealthy"
    fi
}

# Check API health
check_api_health() {
    if [[ "${COMPONENT}" != "all" && "${COMPONENT}" != "api" ]]; then
        return 0
    fi
    
    log_check "Checking API health..."
    
    local status="healthy"
    local retries=0
    
    while [[ ${retries} -lt ${MAX_RETRIES} ]]; do
        # Check API health endpoint
        if curl -sf --max-time "${TIMEOUT}" "${API_URL}/health" > /dev/null; then
            log_check "API health endpoint: OK"
            break
        else
            log_warning "API health endpoint check failed (attempt $((retries + 1))/${MAX_RETRIES})"
            retries=$((retries + 1))
            if [[ ${retries} -lt ${MAX_RETRIES} ]]; then
                sleep "${RETRY_INTERVAL}"
            fi
        fi
    done
    
    if [[ ${retries} -eq ${MAX_RETRIES} ]]; then
        log_error "API health endpoint is not responding"
        status="unhealthy"
    fi
    
    # Check API readiness
    retries=0
    while [[ ${retries} -lt ${MAX_RETRIES} ]]; do
        if curl -sf --max-time "${TIMEOUT}" "${API_URL}/health/ready" > /dev/null; then
            log_check "API readiness endpoint: OK"
            break
        else
            log_warning "API readiness endpoint check failed (attempt $((retries + 1))/${MAX_RETRIES})"
            retries=$((retries + 1))
            if [[ ${retries} -lt ${MAX_RETRIES} ]]; then
                sleep "${RETRY_INTERVAL}"
            fi
        fi
    done
    
    if [[ ${retries} -eq ${MAX_RETRIES} ]]; then
        log_error "API readiness endpoint is not responding"
        status="unhealthy"
    fi
    
    # Check API database connectivity
    retries=0
    while [[ ${retries} -lt ${MAX_RETRIES} ]]; do
        if curl -sf --max-time "${TIMEOUT}" "${API_URL}/health/db" > /dev/null; then
            log_check "API database connectivity: OK"
            break
        else
            log_warning "API database connectivity check failed (attempt $((retries + 1))/${MAX_RETRIES})"
            retries=$((retries + 1))
            if [[ ${retries} -lt ${MAX_RETRIES} ]]; then
                sleep "${RETRY_INTERVAL}"
            fi
        fi
    done
    
    if [[ ${retries} -eq ${MAX_RETRIES} ]]; then
        log_error "API database connectivity is not working"
        status="unhealthy"
    fi
    
    HEALTH_RESULTS["api"]="${status}"
    if [[ "${status}" == "unhealthy" ]]; then
        OVERALL_STATUS="unhealthy"
    fi
}

# Check Frontend health
check_frontend_health() {
    if [[ "${COMPONENT}" != "all" && "${COMPONENT}" != "frontend" ]]; then
        return 0
    fi
    
    log_check "Checking Frontend health..."
    
    local status="healthy"
    local retries=0
    
    while [[ ${retries} -lt ${MAX_RETRIES} ]]; do
        # Check Frontend health endpoint
        if curl -sf --max-time "${TIMEOUT}" "${FRONTEND_URL}/health" > /dev/null; then
            log_check "Frontend health endpoint: OK"
            break
        else
            log_warning "Frontend health endpoint check failed (attempt $((retries + 1))/${MAX_RETRIES})"
            retries=$((retries + 1))
            if [[ ${retries} -lt ${MAX_RETRIES} ]]; then
                sleep "${RETRY_INTERVAL}"
            fi
        fi
    done
    
    if [[ ${retries} -eq ${MAX_RETRIES} ]]; then
        log_error "Frontend health endpoint is not responding"
        status="unhealthy"
    fi
    
    # Check Frontend main page
    retries=0
    while [[ ${retries} -lt ${MAX_RETRIES} ]]; do
        if curl -sf --max-time "${TIMEOUT}" "${FRONTEND_URL}/" > /dev/null; then
            log_check "Frontend main page: OK"
            break
        else
            log_warning "Frontend main page check failed (attempt $((retries + 1))/${MAX_RETRIES})"
            retries=$((retries + 1))
            if [[ ${retries} -lt ${MAX_RETRIES} ]]; then
                sleep "${RETRY_INTERVAL}"
            fi
        fi
    done
    
    if [[ ${retries} -eq ${MAX_RETRIES} ]]; then
        log_error "Frontend main page is not responding"
        status="unhealthy"
    fi
    
    HEALTH_RESULTS["frontend"]="${status}"
    if [[ "${status}" == "unhealthy" ]]; then
        OVERALL_STATUS="unhealthy"
    fi
}

# Check external dependencies
check_external_dependencies() {
    log_check "Checking external dependencies..."
    
    local status="healthy"
    
    # Check PostgreSQL
    local postgres_pods=$(kubectl --kubeconfig="${KUBECONFIG}" get pods -n "${NAMESPACE}" \
        -l app=postgresql --field-selector=status.phase=Running -o name 2>/dev/null | wc -l)
    
    if [[ ${postgres_pods} -eq 0 ]]; then
        log_warning "PostgreSQL pods not found or not running"
        status="degraded"
    else
        log_check "PostgreSQL: ${postgres_pods} pods running"
    fi
    
    # Check Redis
    local redis_pods=$(kubectl --kubeconfig="${KUBECONFIG}" get pods -n "${NAMESPACE}" \
        -l app=redis --field-selector=status.phase=Running -o name 2>/dev/null | wc -l)
    
    if [[ ${redis_pods} -eq 0 ]]; then
        log_warning "Redis pods not found or not running"
        status="degraded"
    else
        log_check "Redis: ${redis_pods} pods running"
    fi
    
    # Check MinIO
    local minio_pods=$(kubectl --kubeconfig="${KUBECONFIG}" get pods -n "${NAMESPACE}" \
        -l app=minio --field-selector=status.phase=Running -o name 2>/dev/null | wc -l)
    
    if [[ ${minio_pods} -eq 0 ]]; then
        log_warning "MinIO pods not found or not running"
        status="degraded"
    else
        log_check "MinIO: ${minio_pods} pods running"
    fi
    
    HEALTH_RESULTS["dependencies"]="${status}"
    if [[ "${status}" == "unhealthy" ]]; then
        OVERALL_STATUS="unhealthy"
    elif [[ "${status}" == "degraded" && "${OVERALL_STATUS}" == "healthy" ]]; then
        OVERALL_STATUS="degraded"
    fi
}

# Output results
output_results() {
    case "${OUTPUT_FORMAT}" in
        "json")
            output_json
            ;;
        "prometheus")
            output_prometheus
            ;;
        *)
            output_text
            ;;
    esac
}

# Output in text format
output_text() {
    if [[ "${QUIET}" == "true" ]]; then
        exec 1>&2
    fi
    
    echo ""
    echo "EAM Health Check Results"
    echo "========================"
    echo "Environment: ${ENVIRONMENT}"
    echo "Namespace: ${NAMESPACE}"
    echo "Timestamp: $(date)"
    echo "Overall Status: ${OVERALL_STATUS^^}"
    echo ""
    
    for component in "${!HEALTH_RESULTS[@]}"; do
        local status="${HEALTH_RESULTS[$component]}"
        local icon=""
        case "${status}" in
            "healthy") icon="✅" ;;
            "degraded") icon="⚠️" ;;
            "unhealthy") icon="❌" ;;
        esac
        echo "${icon} ${component^}: ${status^^}"
    done
    echo ""
}

# Output in JSON format
output_json() {
    if [[ "${QUIET}" == "true" ]]; then
        exec 1>&2
    fi
    
    echo "{"
    echo "  \"environment\": \"${ENVIRONMENT}\","
    echo "  \"namespace\": \"${NAMESPACE}\","
    echo "  \"timestamp\": \"$(date -Iseconds)\","
    echo "  \"overall_status\": \"${OVERALL_STATUS}\","
    echo "  \"components\": {"
    
    local first=true
    for component in "${!HEALTH_RESULTS[@]}"; do
        if [[ "${first}" == "false" ]]; then
            echo ","
        fi
        echo -n "    \"${component}\": \"${HEALTH_RESULTS[$component]}\""
        first=false
    done
    echo ""
    echo "  }"
    echo "}"
}

# Output in Prometheus format
output_prometheus() {
    if [[ "${QUIET}" == "true" ]]; then
        exec 1>&2
    fi
    
    echo "# HELP eam_health_status EAM component health status"
    echo "# TYPE eam_health_status gauge"
    
    for component in "${!HEALTH_RESULTS[@]}"; do
        local status="${HEALTH_RESULTS[$component]}"
        local value=0
        case "${status}" in
            "healthy") value=1 ;;
            "degraded") value=0.5 ;;
            "unhealthy") value=0 ;;
        esac
        echo "eam_health_status{environment=\"${ENVIRONMENT}\",component=\"${component}\"} ${value}"
    done
    
    local overall_value=0
    case "${OVERALL_STATUS}" in
        "healthy") overall_value=1 ;;
        "degraded") overall_value=0.5 ;;
        "unhealthy") overall_value=0 ;;
    esac
    echo "eam_health_status{environment=\"${ENVIRONMENT}\",component=\"overall\"} ${overall_value}"
}

# Main health check function
main() {
    # Check prerequisites
    check_prerequisites
    
    # Perform health checks
    check_kubernetes_resources
    check_api_health
    check_frontend_health
    check_external_dependencies
    
    # Output results
    output_results
    
    # Exit with appropriate code
    case "${OVERALL_STATUS}" in
        "healthy")
            exit 0
            ;;
        "degraded")
            exit 1
            ;;
        "unhealthy")
            exit 2
            ;;
    esac
}

# Run main function
main "$@"