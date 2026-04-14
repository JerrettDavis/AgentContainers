# End-to-End Container Validation

This document describes the e2e test harness that validates generated
AgentContainers images and compose stacks by building them locally and
running manifest-defined validation commands inside containers.

---

## Overview

The e2e harness is **manifest-driven**: the generator emits a JSON test plan
(`emit-e2e-plan` command) containing every image to build, its build context,
and the validation commands to run. The test scripts consume this plan — no
hard-coded tool checks.

### What is tested

| Layer | Coverage | Validation source |
|-------|----------|-------------------|
| **Base images** (python, node-bun, dotnet, rust) | Build + runtime validation + common tools | `definitions/bases/*.yaml` + `definitions/common-tools/default.yaml` |
| **Agent overlay images** (e.g., node-bun-claude) | Build + agent CLI validation | `definitions/agents/*.yaml` |
| **Compose readiness** | Service start + healthcheck + exec commands | Generated compose + dynamic e2e stack |
| **Combo images** | _Skipped_ — combo Dockerfiles don't include common tools layer (known gap) | — |

### Test scopes

| Scope | What runs | Approximate time |
|-------|-----------|-----------------|
| `quick` | 1 base (python) + 1 agent (node-bun-claude) + compose health | ~5 min |
| `bases` | All 4 base images | ~10 min |
| `agents` | All bases + all agent overlays on bases | ~20 min |
| `compose` | All bases + agents + compose service readiness | ~25 min |
| `full` | Everything above | ~25 min |

---

## Prerequisites

- **Docker** (Docker Engine ≥ 20 or Docker Desktop)
- **Docker Compose** v2 (`docker compose` plugin)
- **.NET 10 SDK** (to build the generator)
- **jq** (for the bash script; not needed for PowerShell)
- **Internet access** (to pull base images like `python:3.12-slim-bookworm`)

---

## Running locally

### Bash (Linux / macOS / WSL / Git Bash)

```bash
# Quick smoke test (default)
./scripts/run-e2e.sh

# Full base image validation
./scripts/run-e2e.sh bases

# Test specific images
./scripts/run-e2e.sh bases --filter python,node-bun

# Full suite, keep images after test
./scripts/run-e2e.sh full --no-cleanup

# Agent tests only (builds bases first)
./scripts/run-e2e.sh agents --filter node-bun-claude
```

### PowerShell (Windows)

```powershell
# Quick smoke test
.\scripts\run-e2e.ps1

# Full base validation
.\scripts\run-e2e.ps1 -Scope bases

# Filtered run
.\scripts\run-e2e.ps1 -Scope agents -Filter node-bun-claude -NoCleanup
```

---

## CI integration

The e2e workflow (`.github/workflows/e2e.yml`) runs:

- **PR**: `quick` scope — builds python base + claude agent overlay + compose
  health check. Fast feedback, ~5 min.
- **Main push**: `bases` scope — builds all 4 base images with full validation.
- **Manual dispatch**: Any scope with optional filter.

The e2e workflow is separate from the main CI (`ci.yml`) to avoid blocking
PRs on long Docker builds. CI continues to run unit tests, manifest validation,
and drift checks.

---

## Architecture

```
scripts/run-e2e.sh          Bash e2e runner
scripts/run-e2e.ps1         PowerShell e2e runner
    │
    ├── dotnet run ... emit-e2e-plan    ← Generator outputs JSON test plan
    │       │
    │       ├── bases[]        Build context + validation commands
    │       ├── agents[]       Build context + base dependency + validation
    │       └── compose_stacks[]   Compose paths + required images
    │
    ├── docker build           Build each image from generated/ context
    ├── docker run --rm        Execute validation commands in containers
    └── docker compose up/down Test service readiness + healthchecks
```

### Agent overlay Dockerfiles

The generator now produces agent overlay Dockerfiles at
`generated/docker/agents/{base}-{agent}/Dockerfile`. These layer the agent
CLI install on top of a base image:

```dockerfile
ARG BASE_IMAGE=agentcontainers/node-bun:latest
FROM ${BASE_IMAGE}

USER root
RUN npm install -g @anthropic-ai/claude-code
USER dev
```

The `BASE_IMAGE` build arg lets you swap the base registry for local vs
published images:

```bash
# Local (default)
docker build -t agentcontainers/node-bun-claude:latest generated/docker/agents/node-bun-claude/

# From registry
docker build --build-arg BASE_IMAGE=ghcr.io/agentcontainers/node-bun:latest \
  -t ghcr.io/agentcontainers/node-bun-claude:latest generated/docker/agents/node-bun-claude/
```

---

## Adding new e2e coverage

1. Add `validation.commands` to your manifest YAML — the e2e plan picks them
   up automatically.
2. Run `dotnet run --project src/AgentContainers.Generator -- generate` to
   regenerate artifacts (including any new agent Dockerfiles).
3. Run `./scripts/run-e2e.sh` to verify.

No changes to the e2e scripts are needed for new bases, agents, or tool packs.

---

## Known limitations

- **Combo images are skipped** in e2e: the generated combo Dockerfiles currently
  omit the common-tools layer and user setup, making them non-buildable in
  isolation. This is a pre-existing generator gap.
- **Agent CLI version validation** uses `|| true` for some agents (copilot,
  openclaw) because their packages may not be published or may have different
  CLI names. These are soft checks.
- **Compose tests** use a synthetic health-check service rather than the
  full gateway-headroom stack, which requires the third-party `headroom/headroom`
  sidecar image.
- **Rust base** builds are slow (~5 min) due to `cargo install` compilation.
  Excluded from `quick` scope.
- **Bun on node-bun** base: the `bun --version` validation currently fails because
  the bun installer targets `$HOME/.bun` (root during build) but the image
  switches to the `dev` user. This is a pre-existing manifest bug, not an e2e
  issue. The e2e harness correctly surfaces it.
