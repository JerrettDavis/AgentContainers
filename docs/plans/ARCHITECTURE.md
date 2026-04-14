# ARCHITECTURE.md

## AgentContainers — System Architecture

---

## 1. System Overview

AgentContainers is a **matrix-driven container generation and composition system**.

The system takes a set of declarative YAML manifests describing image layers and produces:
- Static, committed Dockerfiles (one per image variant)
- Compose fragments and example stacks
- Generated documentation tables
- Image metadata manifests

The core loop is: **define → generate → validate → build → publish**.

---

## 2. Layer Model

Images are modeled as ordered compositions of up to five layer types. Layers are additive; later layers build on earlier ones and cannot remove earlier content.

```
┌──────────────────────────────────────────────────┐
│  Tool Pack Overlay  (optional, additive)          │
├──────────────────────────────────────────────────┤
│  Agent Overlay      (one or more agents)          │
├──────────────────────────────────────────────────┤
│  Combo Runtime      (multi-language, optional)    │
├──────────────────────────────────────────────────┤
│  Base Runtime       (single language/platform)    │
├──────────────────────────────────────────────────┤
│  Common Tools       (universal utilities)         │
└──────────────────────────────────────────────────┘
```

### Layer 1 — Common Tools

Installed on every image. Contains broadly useful shell utilities that agents and humans both depend on.

Canonical set (v1):
`git`, `curl`, `wget`, `less`, `sudo`, `jq`, `nano`, `vim`, `unzip`, `gnupg2`,
`man-db`, `ripgrep`, `fd-find`, `procps`, `ca-certificates`, `bash-completion`,
`zsh`, `tar`, `xz-utils`, `tree`, `htop`, `openssh-client`

- Versioned as a named definition (`common-tools/default.yaml`)
- Can be overridden per image for minimal-footprint variants
- All tools are validated post-build by the runtime smoke test harness

### Layer 2 — Base Runtime

A primary language environment. Each base declares:
- Upstream `FROM` image source and pinned digest strategy
- Package manager and install steps
- PATH exports and cache paths
- Expected validation commands (e.g., `node --version`)
- Shell assumptions (default: `bash`; override to `zsh` or `sh`)
- Resource hints (memory, storage footprint class)

**v1 base images:** `node-bun`, `python`, `dotnet`
**Deferred:** `rust`, `cpp`, `haskell`

### Layer 3 — Combo Runtime

A pre-declared union of multiple base runtimes, produced by the generator from ordered base fragments rather than hand-authored Dockerfiles.

Combos declare which bases they include and the merge order (matters for PATH and env precedence).

**v1 combos:** `node-py-dotnet`

Rules:
- Combos must explicitly list their constituent bases
- Bases within a combo must be individually compatible
- Combos may declare additional cross-runtime utilities (e.g., `pipx` for Python tools inside a Node-primary image)

### Layer 4 — Agent Overlay

Installs and configures an agent provider on top of any compatible runtime.

Each agent manifest declares:
- Install mechanism (npm global, pip, curl-to-bin, package manager)
- Runtime dependency requirements (`requires: [node>=20]`)
- Expected environment variables (with descriptions and sensitivity flags)
- Default config directories and persistence paths
- Health probe strategy (command, HTTP, or file-exists)
- Optional shell helper scripts
- Known caveats and privilege requirements
- Extension hooks (plugin directories, sidecar coordination)

**v1 agents:** `claude`, `openclaw`
**Deferred:** `opencode`, `gemini`, `codex`

### Layer 5 — Tool Pack Overlay

Optional, additive layers that add domain-specific utilities. Tool packs must declare:
- Target compatibility (which bases/combos they require)
- Packages added
- Environment variables introduced
- Compose capabilities provided (e.g., sidecar service definition, port exposures)
- Known conflicts with other packs

**v1 packs:** `headroom`
**Deferred:** `gh-azure`, `discord`, `build-tools`, `diagnostics`, `database`

---

## 3. Generation Pipeline

### 3.1 Inputs

```
definitions/
  common-tools/
    default.yaml
  bases/
    node-bun.yaml
    python.yaml
    dotnet.yaml
  combos/
    node-py-dotnet.yaml
  agents/
    claude.yaml
    openclaw.yaml
  toolpacks/
    headroom.yaml
  tag-policies/
    default.yaml
  compose-stacks/
    solo-claude.yaml
    dual-agent.yaml
    gateway-headroom.yaml
templates/
  dockerfiles/
    base.dockerfile.scriban
    combo.dockerfile.scriban
    agent.dockerfile.scriban
    toolpack.dockerfile.scriban
  compose/
    service.yaml.scriban
    network.yaml.scriban
  docs/
    image-row.md.scriban
```

### 3.2 Generator Responsibilities

The generator (`src/AgentContainers.Generator`) performs the following steps in order:

1. **Load** all manifests from `definitions/`
2. **Validate** each manifest against its JSON Schema
3. **Resolve** inheritance chains and layer composition order
4. **Expand** the combination matrix (respecting explicit filters and compatibility rules)
5. **Render** templates per combination using Scriban
6. **Write** outputs to `generated/` with stable, predictable paths
7. **Write** a generation report (`generated/manifest.json`) listing every artifact produced

### 3.3 Outputs

```
generated/
  dockerfiles/
    bases/
      node-bun/Dockerfile
      python/Dockerfile
      dotnet/Dockerfile
    combos/
      node-py-dotnet/Dockerfile
    agents/
      node-bun-claude/Dockerfile
      node-bun-openclaw/Dockerfile
      node-py-dotnet-claude/Dockerfile
      node-py-dotnet-openclaw/Dockerfile
      ... (matrix expansion)
    toolpacks/
      node-bun-claude-headroom/Dockerfile
      ...
  compose/
    fragments/
      claude-service.yaml
      openclaw-service.yaml
      headroom-service.yaml
    examples/
      solo-claude/docker-compose.yaml
      dual-agent/docker-compose.yaml
      gateway-headroom/docker-compose.yaml
  manifests/
    image-catalog.json
    generation-report.json
  docs/
    image-table.md
```

### 3.4 Determinism Rules

- Generation output is sorted and formatted consistently (normalized YAML/Dockerfile whitespace)
- Generator must produce byte-identical output on repeated runs given the same inputs
- Output paths are derived from entity IDs, not display names
- The generator emits a content hash per artifact for drift detection

### 3.5 Drift Detection

CI runs the generator and compares output to committed files using `git diff --exit-code generated/`. A non-empty diff fails the workflow. This ensures definitions and artifacts stay synchronized.

---

## 4. Compatibility Model

Compatibility rules are encoded in manifests, not in generator logic. This keeps the generator general and pushes constraints to the definition layer.

### Rule Types

| Rule | Location | Semantics |
|---|---|---|
| `requires` | agent or toolpack manifest | List of base capabilities that must be present |
| `conflicts` | agent or toolpack manifest | List of other agents or packs that cannot coexist |
| `only_with` | combo manifest | Restricts which agents a combo supports |
| `min_version` | inside a `requires` entry | Minimum runtime version required |

### Capability System

Each base and combo emits a set of **capability tokens** (e.g., `node`, `node>=20`, `python`, `dotnet`, `bun`). Agent and toolpack manifests express their requirements against these tokens. The generator rejects combinations where requirements cannot be satisfied.

Example:
```yaml
# agents/claude.yaml
requires:
  - node>=18
conflicts: []
```

```yaml
# bases/python.yaml
provides:
  - python
  - python>=3.11
```

Attempting to combine `claude` with `python`-only base fails with a clear error: `claude requires node>=18; python base does not provide it`.

---

## 5. Repo Layout

```
AgentContainers/
  definitions/
    common-tools/
    bases/
    combos/
    agents/
    toolpacks/
    compose-stacks/
    tag-policies/
  templates/
    dockerfiles/
    compose/
    docs/
  generated/           ← committed, machine-owned
    dockerfiles/
    compose/
    manifests/
    docs/
  src/
    AgentContainers.Generator/   ← CLI tool, .NET 8
    AgentContainers.Core/        ← shared models, schemas, resolvers
    AgentContainers.Validation/  ← validation harness
  schemas/
    base.schema.json
    combo.schema.json
    agent.schema.json
    toolpack.schema.json
    compose-stack.schema.json
    tag-policy.schema.json
  scripts/
    generate.ps1        ← Windows-friendly entrypoint
    generate.sh         ← Linux/macOS entrypoint
    validate.ps1
    validate.sh
    build-local.ps1
    build-local.sh
  compose/             ← source-of-truth compose stacks (symlinked or copied from generated/)
  docs/
    plans/             ← this directory
    images/
    agents/
    toolpacks/
  .github/
    workflows/
      generate-and-validate.yaml
      build-matrix.yaml
      publish.yaml
  VISION.md
  CONTRIBUTING.md
```

The boundary between `definitions/` (human-authored) and `generated/` (machine-authored) is strict. No human should hand-edit files under `generated/`.

---

## 6. Extension Points

### Adding a New Base Runtime

1. Create `definitions/bases/<id>.yaml` conforming to `schemas/base.schema.json`
2. Add a template fragment if the runtime requires non-trivial install logic
3. The generator picks it up automatically in the next generation run

### Adding a New Agent Overlay

1. Create `definitions/agents/<id>.yaml` with full `requires`, `env`, `health`, and `mounts` blocks
2. Add agent-specific template logic if necessary
3. Add runtime smoke test expectations to the agent manifest

### Adding a New Tool Pack

1. Create `definitions/toolpacks/<id>.yaml`
2. Declare compatibility constraints
3. Optional: add a compose service fragment if the pack needs a sidecar

### Adding a New Combo

1. Create `definitions/combos/<id>.yaml` listing constituent bases in order
2. No code changes required if bases already exist

### Adding a Custom Profile

1. Create `definitions/profiles/<id>.yaml` listing a subset of images to build/publish
2. Pass `--profile <id>` to the generator or build scripts

### Custom Organization Overlays

External repos can import `AgentContainers.Core` as a library and author their own generator entrypoints against private definition directories. This is the intended extensibility path for organizations that cannot publish their internal tooling definitions publicly.

---

## 7. Tagging Strategy

### Canonical Tag Format

```
<runtime-family>-<agent-set>[-<pack-set>]:<version>
```

Examples:
- `node-bun-claude:latest`
- `node-py-dotnet-openclaw-headroom:2026.04.0`
- `python-claude:2026.04.0`

### Versioning

- `latest` — always tracks the most recent stable build from the default branch
- `<YYYY.MM.PATCH>` — CalVer, allows chronological sorting
- `<git-sha>` — always available as a supplementary tag for exact pinning

### OCI Labels (Required on All Published Images)

```
org.opencontainers.image.title
org.opencontainers.image.description
org.opencontainers.image.version
org.opencontainers.image.revision       (git SHA)
org.opencontainers.image.created
org.opencontainers.image.source         (repo URL)
org.opencontainers.image.licenses
dev.agentcontainers.runtime-family
dev.agentcontainers.agents
dev.agentcontainers.toolpacks
dev.agentcontainers.generator-version
dev.agentcontainers.manifest-hash       (hash of source definitions)
```

---

## 8. Observability Design

### Build-Time

- Generator emits `generated/manifests/generation-report.json` with every artifact, its hash, and the source definitions that contributed to it
- CI build steps emit structured annotations (GitHub Actions summary tables)
- Vulnerability scan results are stored as artifacts per run (Trivy or Grype)

### Runtime

- Every image includes a `/healthcheck` command or equivalent declared in its manifest
- Startup scripts emit structured JSON log lines (at minimum: `agent_started`, `version`, `config_path`)
- Compose examples include `healthcheck:` blocks and `depends_on: condition: service_healthy` wiring
- Optional: metrics endpoint or diagnostics script per agent (declared in overlay manifest)

### Provenance (v1 partial, full post-v1)

- v1: OCI labels + generation-report.json
- Post-v1: SLSA provenance attestation, SBOM (Syft or Docker BuildKit SBOM support), cosign signing

---

## 9. Security Design

### Principles

- No secrets baked into images; all credentials injected via environment variables or bind-mounted files
- Non-root user by default in all images; root only available via explicit `--user root` override
- Privileged modes (e.g., Docker-in-Docker) must be declared in the manifest as a `privileged_modes` block with documented rationale
- Minimal surface area: base images install nothing beyond what their manifest declares

### Secret Injection Patterns

| Pattern | Use Case |
|---|---|
| `ENV` in compose (from host env) | API keys, tokens for development |
| `env_file:` in compose | Multi-key config for local use |
| Bind-mounted credential files | Long-lived creds (e.g., `~/.config/claude`) |
| Docker secrets (swarm/future) | Production hardened deployments |

### Non-Root User Convention

All images create a `dev` user (UID 1000) and set it as the default `USER`. Sudo access is available but requires explicit enablement via manifest flag.

### Volume Mount Clarity

Every mount a container uses must be declared in its manifest under `mounts:` with a description and `required: true/false`. Compose stacks derived from manifests include correct volume definitions automatically.
