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
#   quick     Build + validate one base (node-bun) + one agent + one compose path
#   bases     Build + validate all base images
#   agents    Build + validate all agent overlay images (requires bases)
#   compose   Test compose stack service readiness
#   full      Run everything (bases, combos, agents, tool-packs, compose)
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
TOOLPACK_COUNT=$(echo "$E2E_PLAN" | jq '.tool_packs | length')
COMPOSE_COUNT=$(echo "$E2E_PLAN" | jq '.compose_stacks | length')
log_info "Plan: $BASE_COUNT bases, $COMBO_COUNT combos, $AGENT_COUNT agents, $TOOLPACK_COUNT tool-packs, $COMPOSE_COUNT compose stacks"

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

        # Skip heavy images in quick mode — build node-bun (needed by quick agent test)
        if [[ "$SCOPE" == "quick" && "$id" != "node-bun" ]]; then
            log_skip "Base $id (quick scope — only node-bun)"
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
# Test: Combo Images
# ===========================================================================
test_combos() {
    log_header "Testing Combo Images"
    local count
    count=$(echo "$E2E_PLAN" | jq '.combos | length')

    for i in $(seq 0 $((count - 1))); do
        local id display_name context tag size_class
        id=$(echo "$E2E_PLAN" | jq -r ".combos[$i].id")
        display_name=$(echo "$E2E_PLAN" | jq -r ".combos[$i].display_name")
        context=$(echo "$E2E_PLAN" | jq -r ".combos[$i].build_context")
        tag=$(echo "$E2E_PLAN" | jq -r ".combos[$i].tag")
        size_class=$(echo "$E2E_PLAN" | jq -r ".combos[$i].size_class")

        if ! matches_filter "$id"; then
            log_skip "Combo $id (filtered out)"
            continue
        fi

        echo -e "\n  ${BOLD}Combo: $display_name ($id)${NC} [${size_class}]"

        if build_image "$context" "$tag"; then
            log_pass "Build: $tag"

            local cmd_count
            cmd_count=$(echo "$E2E_PLAN" | jq ".combos[$i].validation_commands | length")
            for j in $(seq 0 $((cmd_count - 1))); do
                local cmd
                cmd=$(echo "$E2E_PLAN" | jq -r ".combos[$i].validation_commands[$j]")
                validate_in_container "$tag" "$cmd"
            done
        else
            log_fail "Build: $tag"
        fi
    done
}

# ===========================================================================
# Test: Tool Pack Overlay Images
# ===========================================================================
test_tool_packs() {
    log_header "Testing Tool Pack Overlay Images"
    local count
    count=$(echo "$E2E_PLAN" | jq '.tool_packs | length')

    for i in $(seq 0 $((count - 1))); do
        local id pack_id base_id display_name context tag base_tag size_class
        id=$(echo "$E2E_PLAN" | jq -r ".tool_packs[$i].id")
        pack_id=$(echo "$E2E_PLAN" | jq -r ".tool_packs[$i].pack_id")
        base_id=$(echo "$E2E_PLAN" | jq -r ".tool_packs[$i].base_id")
        display_name=$(echo "$E2E_PLAN" | jq -r ".tool_packs[$i].display_name")
        context=$(echo "$E2E_PLAN" | jq -r ".tool_packs[$i].build_context")
        tag=$(echo "$E2E_PLAN" | jq -r ".tool_packs[$i].tag")
        base_tag=$(echo "$E2E_PLAN" | jq -r ".tool_packs[$i].base_tag")
        size_class=$(echo "$E2E_PLAN" | jq -r ".tool_packs[$i].size_class")

        if ! matches_filter "$id"; then
            log_skip "ToolPack $id (filtered out)"
            continue
        fi

        echo -e "\n  ${BOLD}ToolPack: $display_name ($id)${NC} [${size_class}]"

        # Verify base image exists
        if ! docker image inspect "$base_tag" > /dev/null 2>&1; then
            log_skip "ToolPack $id: base image $base_tag not found (build bases first)"
            continue
        fi

        if build_image "$context" "$tag"; then
            log_pass "Build: $tag"

            local cmd_count
            cmd_count=$(echo "$E2E_PLAN" | jq ".tool_packs[$i].validation_commands | length")
            for j in $(seq 0 $((cmd_count - 1))); do
                local cmd
                cmd=$(echo "$E2E_PLAN" | jq -r ".tool_packs[$i].validation_commands[$j]")
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

    local count
    count=$(echo "$E2E_PLAN" | jq '.compose_stacks | length')

    # Phase 1: Validate syntax of all generated compose files
    for i in $(seq 0 $((count - 1))); do
        local id compose_path
        id=$(echo "$E2E_PLAN" | jq -r ".compose_stacks[$i].id")
        compose_path=$(echo "$E2E_PLAN" | jq -r ".compose_stacks[$i].compose_path")

        local full_path="$REPO_ROOT/$compose_path"
        if [[ ! -f "$full_path" ]]; then
            log_skip "Compose config: $id — file not found"
            continue
        fi

        echo -e "\n  ${BOLD}Compose config: $id${NC}"

        # Create a dummy .env file if the compose stack references env_file
        local compose_dir
        compose_dir=$(dirname "$full_path")
        local created_env=false
        if [[ ! -f "$compose_dir/.env" ]]; then
            touch "$compose_dir/.env"
            created_env=true
        fi

        # Validate syntax (set dummy env vars for all known required secrets)
        if ANTHROPIC_API_KEY=e2e-test GITHUB_TOKEN=e2e-test OPENAI_API_KEY=e2e-test \
           OPENCLAW_API_KEY=e2e-test CODEX_API_KEY=e2e-test \
           docker compose -f "$full_path" config > /dev/null 2>&1; then
            log_pass "Compose config: $id"
        else
            log_fail "Compose config: $id"
        fi

        # Cleanup temp .env
        if [[ "$created_env" == "true" ]]; then
            rm -f "$compose_dir/.env"
        fi
    done

    # Phase 2: Runtime test using generated solo-claude stack with locally built images
    local agent_tag="agentcontainers/node-bun-claude:latest"
    if ! docker image inspect "$agent_tag" > /dev/null 2>&1; then
        log_skip "Compose runtime: agent image $agent_tag not available (run agents scope first)"
        return
    fi

    local solo_claude_path="$REPO_ROOT/generated/compose/stacks/solo-claude/docker-compose.yaml"
    if [[ ! -f "$solo_claude_path" ]]; then
        log_skip "Compose runtime: solo-claude stack not found"
        return
    fi

    echo -e "\n  ${BOLD}Compose runtime: solo-claude (generated stack)${NC}"

    # Tag local image to match the registry reference in the generated compose file
    docker tag "$agent_tag" "ghcr.io/agentcontainers/node-bun-claude:latest" 2>/dev/null || true
    E2E_TAGS+=("ghcr.io/agentcontainers/node-bun-claude:latest")

    # Create override that keeps the container alive for testing
    local override_file="$REPO_ROOT/generated/compose/stacks/solo-claude/docker-compose.e2e.yaml"
    cat > "$override_file" <<'EOF'
# E2E override — auto-generated, do not commit
services:
  claude:
    command: ["sleep", "infinity"]
    healthcheck:
      test: ["CMD", "node", "--version"]
      interval: 5s
      timeout: 3s
      retries: 10
      start_period: 3s
    restart: "no"
EOF

    local compose_project="e2e-agentcontainers-$$"
    if ! ANTHROPIC_API_KEY=e2e-test \
         docker compose -f "$solo_claude_path" -f "$override_file" -p "$compose_project" up -d 2>/dev/null; then
        log_fail "Compose runtime: stack failed to start"
        docker compose -f "$solo_claude_path" -f "$override_file" -p "$compose_project" down -v 2>/dev/null || true
        rm -f "$override_file"
        return
    fi

    # Wait for healthy status
    log_info "Waiting for service health..."
    local healthy=false
    for attempt in $(seq 1 12); do
        sleep 5
        local status
        status=$(docker compose -f "$solo_claude_path" -f "$override_file" -p "$compose_project" ps --format json 2>/dev/null | jq -r '.Health // "unknown"' 2>/dev/null || echo "unknown")
        if [[ "$status" == "healthy" ]]; then
            healthy=true
            break
        fi
        log_info "  Attempt $attempt/12: status=$status"
    done

    if [[ "$healthy" == "true" ]]; then
        log_pass "Compose runtime: service reached healthy state"

        # Validate commands inside compose service
        if docker compose -f "$solo_claude_path" -f "$override_file" -p "$compose_project" \
            exec -T claude node --version > /dev/null 2>&1; then
            log_pass "Compose runtime: node --version in service"
        else
            log_fail "Compose runtime: node --version in service"
        fi

        if docker compose -f "$solo_claude_path" -f "$override_file" -p "$compose_project" \
            exec -T claude claude --version > /dev/null 2>&1; then
            log_pass "Compose runtime: claude --version in service"
        else
            log_fail "Compose runtime: claude --version in service"
        fi
    else
        log_fail "Compose runtime: service did not reach healthy state within timeout"
        docker compose -f "$solo_claude_path" -f "$override_file" -p "$compose_project" logs 2>/dev/null || true
    fi

    # Cleanup
    log_info "Tearing down compose stack..."
    docker compose -f "$solo_claude_path" -f "$override_file" -p "$compose_project" down -v 2>/dev/null || true
    rm -f "$override_file"
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
        test_combos
        test_agents
        test_tool_packs
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
