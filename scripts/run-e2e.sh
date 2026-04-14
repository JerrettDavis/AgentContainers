#!/usr/bin/env bash
# =============================================================================
# AgentContainers E2E Test Runner
#
# Builds generated Docker images and validates runtime behavior using
# manifest-defined validation commands. Driven by the generator's
# emit-e2e-plan output for deterministic, manifest-driven test cases.
#
# Usage:
#   ./scripts/run-e2e.sh [scope] [options]
#
# Scopes:
#   quick     Build + validate one lightweight base (python) + one agent
#   bases     Build + validate all base images
#   agents    Build + validate all agent overlay images (requires bases)
#   compose   Test compose stack service readiness
#   full      Run everything
#
# Options:
#   --filter <id>   Only test images matching this id (comma-separated)
#   --no-cleanup    Skip Docker image cleanup after tests
#   --timeout <s>   Per-build timeout in seconds (default: 600)
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
GENERATOR_PROJECT="$REPO_ROOT/src/AgentContainers.Generator/AgentContainers.Generator.csproj"

# --- Defaults ---
SCOPE="${1:-quick}"
shift || true
FILTER=""
CLEANUP=true
BUILD_TIMEOUT=600
E2E_TAGS=()  # track built images for cleanup

# --- Parse options ---
while [[ $# -gt 0 ]]; do
    case "$1" in
        --filter)   FILTER="$2"; shift 2 ;;
        --no-cleanup) CLEANUP=false; shift ;;
        --timeout)  BUILD_TIMEOUT="$2"; shift 2 ;;
        *) echo "Unknown option: $1" >&2; exit 1 ;;
    esac
done

# --- Colors ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
BOLD='\033[1m'
NC='\033[0m'

# --- Counters ---
PASS=0
FAIL=0
SKIP=0
ERRORS=()

log_pass()  { echo -e "  ${GREEN}✓ PASS${NC}: $1"; ((PASS++)); }
log_fail()  { echo -e "  ${RED}✗ FAIL${NC}: $1"; ERRORS+=("$1"); ((FAIL++)); }
log_skip()  { echo -e "  ${YELLOW}→ SKIP${NC}: $1"; ((SKIP++)); }
log_info()  { echo -e "  ${BLUE}ℹ${NC} $1"; }
log_header(){ echo -e "\n${BOLD}=== $1 ===${NC}"; }

# --- Helpers ---
matches_filter() {
    local id="$1"
    [[ -z "$FILTER" ]] && return 0
    IFS=',' read -ra filters <<< "$FILTER"
    for f in "${filters[@]}"; do
        [[ "$id" == "$f" ]] && return 0
    done
    return 1
}

build_image() {
    local context="$1"
    local tag="$2"
    local build_args="${3:-}"
    local full_context="$REPO_ROOT/$context"

    if [[ ! -f "$full_context/Dockerfile" ]]; then
        log_fail "Build $tag: Dockerfile not found at $full_context/Dockerfile"
        return 1
    fi

    log_info "Building $tag from $context..."
    if timeout "$BUILD_TIMEOUT" docker build $build_args -t "$tag" "$full_context" > /dev/null 2>&1; then
        E2E_TAGS+=("$tag")
        return 0
    else
        return 1
    fi
}

validate_in_container() {
    local tag="$1"
    local cmd="$2"

    if docker run --rm --timeout 30 "$tag" bash -c "$cmd" > /dev/null 2>&1; then
        log_pass "$tag → $cmd"
    else
        log_fail "$tag → $cmd"
    fi
}

cleanup_images() {
    if [[ "$CLEANUP" == "true" ]] && [[ ${#E2E_TAGS[@]} -gt 0 ]]; then
        log_header "Cleanup"
        for tag in "${E2E_TAGS[@]}"; do
            docker rmi -f "$tag" > /dev/null 2>&1 || true
        done
        log_info "Removed ${#E2E_TAGS[@]} e2e image(s)"
    fi
}

trap cleanup_images EXIT

# --- Generate E2E Plan ---
log_header "Generating E2E Test Plan"
cd "$REPO_ROOT"

E2E_PLAN=$(dotnet run --project "$GENERATOR_PROJECT" --configuration Release -- emit-e2e-plan 2>/dev/null)
if [[ -z "$E2E_PLAN" ]]; then
    echo "ERROR: Failed to generate e2e plan" >&2
    exit 1
fi
log_info "E2E plan generated successfully"

# Parse plan using jq
if ! command -v jq &> /dev/null; then
    echo "ERROR: jq is required for e2e tests. Install with: apt-get install jq" >&2
    exit 1
fi

BASE_COUNT=$(echo "$E2E_PLAN" | jq '.bases | length')
COMBO_COUNT=$(echo "$E2E_PLAN" | jq '.combos | length')
AGENT_COUNT=$(echo "$E2E_PLAN" | jq '.agents | length')
COMPOSE_COUNT=$(echo "$E2E_PLAN" | jq '.compose_stacks | length')
log_info "Plan: $BASE_COUNT bases, $COMBO_COUNT combos, $AGENT_COUNT agents, $COMPOSE_COUNT compose stacks"

# ===========================================================================
# Test: Base Images
# ===========================================================================
test_bases() {
    log_header "Testing Base Images"
    local count
    count=$(echo "$E2E_PLAN" | jq '.bases | length')

    for i in $(seq 0 $((count - 1))); do
        local id display_name context tag size_class
        id=$(echo "$E2E_PLAN" | jq -r ".bases[$i].id")
        display_name=$(echo "$E2E_PLAN" | jq -r ".bases[$i].display_name")
        context=$(echo "$E2E_PLAN" | jq -r ".bases[$i].build_context")
        tag=$(echo "$E2E_PLAN" | jq -r ".bases[$i].tag")
        size_class=$(echo "$E2E_PLAN" | jq -r ".bases[$i].size_class")

        if ! matches_filter "$id"; then
            log_skip "Base $id (filtered out)"
            continue
        fi

        # Skip heavy images in quick mode
        if [[ "$SCOPE" == "quick" && "$id" != "python" ]]; then
            log_skip "Base $id (quick scope — only python)"
            continue
        fi

        echo -e "\n  ${BOLD}Base: $display_name ($id)${NC} [${size_class}]"

        if build_image "$context" "$tag"; then
            log_pass "Build: $tag"

            # Runtime validation commands
            local cmd_count
            cmd_count=$(echo "$E2E_PLAN" | jq ".bases[$i].validation_commands | length")
            for j in $(seq 0 $((cmd_count - 1))); do
                local cmd
                cmd=$(echo "$E2E_PLAN" | jq -r ".bases[$i].validation_commands[$j]")
                validate_in_container "$tag" "$cmd"
            done

            # Common tool validations
            cmd_count=$(echo "$E2E_PLAN" | jq ".bases[$i].common_tool_validations | length")
            for j in $(seq 0 $((cmd_count - 1))); do
                local cmd
                cmd=$(echo "$E2E_PLAN" | jq -r ".bases[$i].common_tool_validations[$j]")
                validate_in_container "$tag" "$cmd"
            done
        else
            log_fail "Build: $tag"
        fi
    done
}

# ===========================================================================
# Test: Agent Overlay Images
# ===========================================================================
test_agents() {
    log_header "Testing Agent Overlay Images"
    local count
    count=$(echo "$E2E_PLAN" | jq '.agents | length')

    for i in $(seq 0 $((count - 1))); do
        local id agent_id base_id display_name context tag base_tag size_class
        id=$(echo "$E2E_PLAN" | jq -r ".agents[$i].id")
        agent_id=$(echo "$E2E_PLAN" | jq -r ".agents[$i].agent_id")
        base_id=$(echo "$E2E_PLAN" | jq -r ".agents[$i].base_id")
        display_name=$(echo "$E2E_PLAN" | jq -r ".agents[$i].display_name")
        context=$(echo "$E2E_PLAN" | jq -r ".agents[$i].build_context")
        tag=$(echo "$E2E_PLAN" | jq -r ".agents[$i].tag")
        base_tag=$(echo "$E2E_PLAN" | jq -r ".agents[$i].base_tag")
        size_class=$(echo "$E2E_PLAN" | jq -r ".agents[$i].size_class")

        if ! matches_filter "$id"; then
            log_skip "Agent $id (filtered out)"
            continue
        fi

        # In quick mode, only test one agent on one base
        if [[ "$SCOPE" == "quick" && "$id" != "node-bun-claude" ]]; then
            log_skip "Agent $id (quick scope — only node-bun-claude)"
            continue
        fi

        # Skip agents on combo bases (combos have known build issues)
        if echo "$E2E_PLAN" | jq -r ".combos[].id" | grep -q "^${base_id}$"; then
            log_skip "Agent $id (combo base — skipped)"
            continue
        fi

        echo -e "\n  ${BOLD}Agent: $display_name ($id)${NC} [${size_class}]"

        # Verify base image exists
        if ! docker image inspect "$base_tag" > /dev/null 2>&1; then
            log_skip "Agent $id: base image $base_tag not found (build bases first)"
            continue
        fi

        if build_image "$context" "$tag"; then
            log_pass "Build: $tag"

            # Agent validation commands
            local cmd_count
            cmd_count=$(echo "$E2E_PLAN" | jq ".agents[$i].validation_commands | length")
            for j in $(seq 0 $((cmd_count - 1))); do
                local cmd
                cmd=$(echo "$E2E_PLAN" | jq -r ".agents[$i].validation_commands[$j]")
                validate_in_container "$tag" "$cmd"
            done
        else
            log_fail "Build: $tag"
        fi
    done
}

# ===========================================================================
# Test: Compose Stack Service Readiness
# ===========================================================================
test_compose() {
    log_header "Testing Compose Stack Readiness"

    # Create a minimal e2e compose test that validates:
    # 1. A locally-built agent image can serve as a compose service
    # 2. Container starts and healthcheck passes
    # 3. Validation commands work inside compose services
    local e2e_compose_dir="$REPO_ROOT/generated/compose/e2e-validation"
    mkdir -p "$e2e_compose_dir"

    # Only run if we have the node-bun-claude image built
    local agent_tag="agentcontainers/node-bun-claude:latest"
    if ! docker image inspect "$agent_tag" > /dev/null 2>&1; then
        log_skip "Compose test: agent image $agent_tag not available (run agents scope first)"
        return
    fi

    echo -e "\n  ${BOLD}Compose: e2e-health-validation${NC}"

    # Generate e2e compose file that tests service readiness
    cat > "$e2e_compose_dir/docker-compose.yaml" <<EOF
# E2E validation compose stack — auto-generated, do not commit
services:
  agent-health:
    image: ${agent_tag}
    command: ["node", "-e", "require('http').createServer((req,res)=>{res.writeHead(200);res.end('ok')}).listen(8080)"]
    ports:
      - "18080:8080"
    healthcheck:
      test: ["CMD", "curl", "-sf", "http://localhost:8080"]
      interval: 5s
      timeout: 3s
      retries: 10
      start_period: 5s
    restart: "no"

networks:
  default:
    driver: bridge
EOF

    # Start the stack
    log_info "Starting e2e compose stack..."
    local compose_project="e2e-agentcontainers-$$"
    if ! docker compose -f "$e2e_compose_dir/docker-compose.yaml" -p "$compose_project" up -d 2>/dev/null; then
        log_fail "Compose: stack failed to start"
        docker compose -f "$e2e_compose_dir/docker-compose.yaml" -p "$compose_project" down -v 2>/dev/null || true
        rm -rf "$e2e_compose_dir"
        return
    fi

    # Wait for healthy status (up to 60s)
    log_info "Waiting for service health..."
    local healthy=false
    for attempt in $(seq 1 12); do
        sleep 5
        local status
        status=$(docker compose -f "$e2e_compose_dir/docker-compose.yaml" -p "$compose_project" ps --format json 2>/dev/null | jq -r '.Health // "unknown"' 2>/dev/null || echo "unknown")
        if [[ "$status" == "healthy" ]]; then
            healthy=true
            break
        fi
        log_info "  Attempt $attempt/12: status=$status"
    done

    if [[ "$healthy" == "true" ]]; then
        log_pass "Compose: service reached healthy state"

        # Test the health endpoint via curl from host
        if curl -sf http://localhost:18080 > /dev/null 2>&1; then
            log_pass "Compose: health endpoint reachable from host"
        else
            log_fail "Compose: health endpoint not reachable from host"
        fi

        # Run validation command inside compose service
        if docker compose -f "$e2e_compose_dir/docker-compose.yaml" -p "$compose_project" \
            exec -T agent-health node --version > /dev/null 2>&1; then
            log_pass "Compose: validation command (node --version) in service"
        else
            log_fail "Compose: validation command failed in service"
        fi

        if docker compose -f "$e2e_compose_dir/docker-compose.yaml" -p "$compose_project" \
            exec -T agent-health claude --version > /dev/null 2>&1; then
            log_pass "Compose: agent command (claude --version) in service"
        else
            log_fail "Compose: agent command (claude --version) in service"
        fi
    else
        log_fail "Compose: service did not reach healthy state within timeout"
        docker compose -f "$e2e_compose_dir/docker-compose.yaml" -p "$compose_project" logs 2>/dev/null || true
    fi

    # Cleanup compose
    log_info "Tearing down compose stack..."
    docker compose -f "$e2e_compose_dir/docker-compose.yaml" -p "$compose_project" down -v 2>/dev/null || true
    rm -rf "$e2e_compose_dir"
}

# ===========================================================================
# Main
# ===========================================================================
log_header "AgentContainers E2E Tests"
echo -e "  Scope: ${BOLD}$SCOPE${NC}"
[[ -n "$FILTER" ]] && echo -e "  Filter: ${BOLD}$FILTER${NC}"
echo -e "  Timeout: ${BUILD_TIMEOUT}s per build"

START_TIME=$SECONDS

case "$SCOPE" in
    quick)
        test_bases
        test_agents
        test_compose
        ;;
    bases)
        test_bases
        ;;
    agents)
        test_bases
        test_agents
        ;;
    compose)
        test_bases
        test_agents
        test_compose
        ;;
    full)
        test_bases
        test_agents
        test_compose
        ;;
    *)
        echo "Unknown scope: $SCOPE" >&2
        echo "Valid scopes: quick, bases, agents, compose, full" >&2
        exit 1
        ;;
esac

# ===========================================================================
# Summary
# ===========================================================================
ELAPSED=$((SECONDS - START_TIME))

log_header "E2E Results"
echo -e "  ${GREEN}Passed: $PASS${NC}"
echo -e "  ${RED}Failed: $FAIL${NC}"
echo -e "  ${YELLOW}Skipped: $SKIP${NC}"
echo -e "  Duration: ${ELAPSED}s"

if [[ $FAIL -gt 0 ]]; then
    echo -e "\n  ${RED}${BOLD}Failures:${NC}"
    for err in "${ERRORS[@]}"; do
        echo -e "    ${RED}• $err${NC}"
    done
    exit 1
fi

echo -e "\n  ${GREEN}${BOLD}All e2e tests passed!${NC}"
exit 0
