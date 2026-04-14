# IMPLEMENTATION-PLAN.md

## AgentContainers — Implementation Plan

---

## Delivery Philosophy

Ship in vertical slices. Each phase must be independently runnable and leave the repo in a better state than it found it. No phase should be a pure infrastructure phase that blocks all visible progress.

The first milestone is intentionally narrow: prove the matrix model works end to end before expanding the combination matrix.

---

## Phase 0 — Repository Bootstrap

**Goal:** Establish the repo scaffold, toolchain decisions, and contribution conventions before any implementation begins.

**Duration estimate:** 1–2 days

### Deliverables

- [ ] Final repo directory structure created (see `ARCHITECTURE.md` §5)
- [ ] `.editorconfig` and `.gitattributes` committed
- [ ] `CONTRIBUTING.md` drafted
- [ ] JSON Schema files created for all manifest types (stubs acceptable)
- [ ] Generator project scaffold created (`src/AgentContainers.Generator`, `src/AgentContainers.Core`)
- [ ] Script entrypoints created (`scripts/generate.ps1`, `scripts/generate.sh`) as no-op stubs
- [ ] Baseline CI workflow committed (run on PR, no build logic yet)

### Acceptance Criteria

- [ ] Repo structure matches `ARCHITECTURE.md` §5 exactly
- [ ] `dotnet build src/` succeeds (even with empty projects)
- [ ] CI workflow runs and passes on an empty PR

### Key Decisions Resolved in This Phase

- Generator language: **.NET 10 (C#)** — enforced by project creation
- Definition format: **YAML** — enforced by schema stub creation
- Template engine: **Scriban** — added as NuGet dependency in this phase

---

## Phase 1 — Core Matrix Model

**Goal:** Implement the manifest schema, generator core, and drift detection. No Docker required yet.

**Duration estimate:** 3–5 days

### Deliverables

- [ ] JSON Schema definitions finalized for all manifest types (see `MANIFEST-MODEL.md`)
- [ ] `AgentContainers.Core` library implementing:
  - Manifest deserialization (YAML → typed C# models)
  - Inheritance/layer resolution
  - Compatibility rule evaluation
  - Capability token matching
  - Combination matrix expansion
- [ ] `AgentContainers.Generator` CLI implementing:
  - `generate` command
  - `validate` command (schema + reference + compatibility checks)
  - `list-matrix` command (dry-run, prints planned combinations)
  - Scriban template rendering
  - Stable output writing with content-hash tracking
- [ ] Sample definitions for at least one base, one agent, and one toolpack (stubs)
- [ ] Generated output folder structure populated from stubs
- [ ] Drift detection CI step: `generate && git diff --exit-code generated/`

### Acceptance Criteria

- [ ] `dotnet run -- validate` passes on valid definitions and fails clearly on invalid ones
- [ ] `dotnet run -- generate` produces byte-identical output on repeated runs
- [ ] Invalid compatibility (e.g., Node-requiring agent on Python-only base) fails with a clear error
- [ ] CI drift detection step fails on a PR that modifies a definition without regenerating
- [ ] `dotnet run -- list-matrix` emits a tabular summary of planned image IDs

### Unit Test Targets (Phase 1)

- Manifest deserialization round-trip
- Inheritance resolution with three levels of nesting
- Compatibility rule evaluation: valid and invalid combinations
- Template rendering for a minimal Dockerfile
- Drift detection: content hash comparison

---

## Phase 2 — Foundational Image Families

**Goal:** Ship real base images and a combo image. Prove the build pipeline works end to end.

**Duration estimate:** 3–5 days

### Deliverables

- [ ] `definitions/bases/node-bun.yaml` — full manifest
- [ ] `definitions/bases/python.yaml` — full manifest
- [ ] `definitions/bases/dotnet.yaml` — full manifest
- [ ] `definitions/combos/node-py-dotnet.yaml`
- [ ] `definitions/common-tools/default.yaml`
- [ ] Generated Dockerfiles for all bases and the combo
- [ ] Runtime smoke test expectations declared in each manifest
- [ ] `scripts/build-local.ps1` — builds a single image by ID
- [ ] CI build workflow: builds all base and combo images on PR
- [ ] Generated `docs/image-table.md` listing all available images

### Acceptance Criteria

- [ ] All base images build from generated Dockerfiles without errors
- [ ] Each base image passes its declared runtime validation commands
- [ ] Common tools are present and correct in each image
- [ ] Combo image contains all three runtimes and passes combined validation
- [ ] Tags are generated in the canonical format
- [ ] OCI labels are present on built images

### Deferred to Later Phases

- Rust, C/C++, Haskell base images
- ARM64 platform support
- Multi-stage builds for size optimization

---

## Phase 3 — Agent Overlays

**Goal:** Install and validate the first two agent providers. Formalize the overlay contract.

**Duration estimate:** 3–5 days

### Deliverables

- [ ] `definitions/agents/claude.yaml` — full manifest with env, mounts, health, install
- [ ] `definitions/agents/openclaw.yaml` — full manifest
- [ ] `definitions/agents/codex.yaml` — full manifest
- [ ] `definitions/agents/copilot.yaml` — full manifest
- [ ] Generated Dockerfiles for all base × agent combinations (v1 matrix)
- [ ] Agent overlay template (`templates/dockerfiles/agent.dockerfile.scriban`)
- [ ] Runtime smoke tests for each agent overlay
- [ ] Per-agent documentation page (`docs/agents/claude.md`, `docs/agents/openclaw.md`, `docs/agents/codex.md`, `docs/agents/copilot.md`)

### v1 Agent Matrix

| Base | Claude | OpenClaw | Codex | Copilot |
|---|---|---|---|---|
| node-bun | ✓ | ✓ | ✓ | ✓ |
| python | — (deferred) | ✓ | — (deferred) | — (deferred) |
| dotnet | — (deferred) | — (deferred) | — (deferred) | — (deferred) |
| node-py-dotnet | ✓ | ✓ | ✓ | ✓ |

### Acceptance Criteria

- [ ] Each agent overlay builds successfully on compatible bases
- [ ] Attempting to build an incompatible combination fails with a clear generator error
- [ ] Each agent image passes agent-specific runtime smoke tests
- [ ] All required environment variables are documented in the manifest
- [ ] Config directories and persistence paths are documented in the manifest

### Key Integration Points (from External Reference Synthesis)

**Claude Code:**
- Likely requires `~/.config/claude` bind mount or equivalent volume for config persistence
- Expects `ANTHROPIC_API_KEY` injected via environment
- Non-root user ergonomics: must run as the `dev` user without elevated permissions
- Interactive and non-interactive modes both supported

**OpenClaw:**
- Service/container pattern: exposes an API or service endpoint
- Requires container networking awareness (compose network, port declaration)
- Configuration driven via environment variables
- Compose-friendly: health endpoint expected

---

## Phase 4 — Tool Packs

**Goal:** Demonstrate that optional tool packs compose cleanly without image explosion.

**Duration estimate:** 2–3 days

### Deliverables

- [ ] `definitions/toolpacks/headroom.yaml` — full manifest
- [ ] Generated Dockerfiles for all agent × headroom combinations in the v1 matrix
- [ ] Headroom compose sidecar fragment (`generated/compose/fragments/headroom-service.yaml`)
- [ ] Tool pack documentation page (`docs/toolpacks/headroom.md`)

### Headroom Integration Specifics

Headroom operates as a **proxy/optimization sidecar**, not installed directly into the agent image. The tool pack manifest must provide:

1. A standalone Headroom sidecar service definition (for compose stacks that want it as a separate container)
2. An optional embedded variant (if Headroom can be co-installed in the agent image)
3. Shared network and environment variable coordination schema
4. Health/readiness dependency declarations so agent containers wait for Headroom to be ready

The `headroom` tool pack installs the Headroom CLI into agent images and documents the required environment variables for routing through it. The sidecar pattern is the recommended v1 deployment model.

### Acceptance Criteria

- [ ] Headroom tool pack builds and passes smoke tests
- [ ] Tool pack compatibility rules are enforced (fails on incompatible base)
- [ ] Headroom compose fragment includes `healthcheck:` and `depends_on: condition: service_healthy`
- [ ] Documentation explains sidecar vs. embedded deployment options

---

## Phase 5 — Compose Topologies

**Goal:** Ship realistic, working Compose examples that prove multi-agent setups are easy to launch.

**Duration estimate:** 3–4 days

### Deliverables

- [ ] `generated/compose/examples/solo-claude/docker-compose.yaml`
- [ ] `generated/compose/examples/dual-agent/docker-compose.yaml`
- [ ] `generated/compose/examples/gateway-headroom/docker-compose.yaml`
- [ ] Compose smoke test (`scripts/validate.ps1 --compose solo-claude`)
- [ ] `docs/compose/` documentation for each example
- [ ] `.env.example` file per stack documenting required environment variables

### Stack Descriptions

**Solo Claude:**
- 1 container: `claude` agent on `node-bun` base
- Mounts: `./workspace:/workspace`, `~/.config/claude:/home/dev/.config/claude`
- Network: none required externally
- Health: Claude availability check

**Dual Agent:**
- 2 containers: `claude` (node-bun) + `openclaw` (node-bun)
- Shared workspace volume
- Separate state volumes
- Shared internal network

**Gateway + Headroom:**
- 3 containers: `openclaw` + `headroom` sidecar + optional `claude`
- OpenClaw depends on Headroom health
- Headroom proxies token requests
- External port exposure on OpenClaw API

### Acceptance Criteria

- [ ] Each stack starts with `docker compose up` using documented prerequisites
- [ ] Health checks are wired with `depends_on: condition: service_healthy` where applicable
- [ ] Persistent volumes are named and documented
- [ ] `.env.example` lists every required variable

---

## Phase 6 — Publishing and Cataloging

**Goal:** Automate image publication and produce a usable catalog.

**Duration estimate:** 2–3 days

### Deliverables

- [ ] `.github/workflows/publish.yaml` — pushes to registry on merge to default branch
- [ ] Tag policy enforcement in generator (`definitions/tag-policies/default.yaml`)
- [ ] `generated/manifests/image-catalog.json` — machine-readable catalog
- [ ] Generated `IMAGE-CATALOG.md` — human-readable catalog table
- [ ] Selective build logic: only rebuild images whose definitions changed (based on manifest hash)
- [ ] Registry configuration: support for GitHub Container Registry (ghcr.io) as primary

### Acceptance Criteria

- [ ] Only changed images are rebuilt and pushed on merge
- [ ] Published images carry full OCI label set
- [ ] Image catalog is regenerated and committed on each publish run
- [ ] Tags include CalVer + `latest` + git SHA

---

## Phase 7 — Hardening and Scale-Out

**Goal:** Security hardening, provenance, community extensibility. Post-v1 milestone.

### Deliverables (Post-v1)

- [ ] Trivy/Grype vulnerability scan integrated into CI (gate on HIGH severity)
- [ ] SBOM generation using Syft or Docker BuildKit SBOM output
- [ ] SLSA provenance attestation (Level 1 minimum)
- [ ] Policy check for risky manifest declarations (e.g., `privileged: true` without documented rationale)
- [ ] Additional base images: `rust`, `cpp`, `haskell`
- [ ] Additional agent overlays: `opencode`, `gemini`
- [ ] Additional tool packs: `gh-azure`, `discord`, `build-tools`, `diagnostics`
- [ ] ARM64 multi-platform builds

---

## Work Tracks and Parallelism

These tracks can proceed in parallel after Phase 0 is complete.

| Track | Owner Focus | Primary Output |
|---|---|---|
| A — Generator & Schema | Core engine, schema design | `src/`, `schemas/`, `definitions/` |
| B — Image Authoring | Manifest writing, Dockerfile validation | `definitions/`, `generated/dockerfiles/` |
| C — Validation & Testing | Smoke tests, CI harness | `src/AgentContainers.Validation/`, CI |
| D — Compose & Examples | Compose stacks, fragment model | `generated/compose/` |
| E — Docs & Catalog | Docs, catalog generation | `docs/`, `IMAGE-CATALOG.md` |

Track A must complete Phase 1 before Track B can produce real artifacts. All other tracks can begin scaffold work immediately.

---

## Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Image matrix explosion | High | Medium | Explicit publish profiles; curated v1 matrix |
| Agent installer instability (upstream changes) | High | High | Pin installer versions; isolate per-agent install fragments |
| Compose complexity / fragile examples | Medium | Medium | Start with simple stacks; add complexity incrementally |
| Generator drift (defs vs. generated) | Medium | High | Drift detection CI step; enforce on every PR |
| Scriban template complexity leaking logic | Medium | Medium | Keep templates logic-minimal; complex decisions in generator |
| Non-root permission surprises per agent | Medium | Medium | Validate each agent at smoke-test time under `dev` user |

---

## First End-to-End Milestone (MVP)

This milestone is the target for the first demo-able state of the system.

**Scope:**
1. Generator runs and produces correct Dockerfiles for `node-bun-claude`, `node-bun-openclaw`, `node-bun-codex`, `node-bun-copilot`, `node-py-dotnet-openclaw-headroom`
2. All three build locally
3. `solo-claude` Compose example starts and passes health check
4. CI drift detection catches a definition change that was not regenerated
5. OCI labels are present on all built images

**Not required for this milestone:** publishing, full matrix, additional agents, SBOM.
