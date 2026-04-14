# AgentContainers

AgentContainers is a **.NET 10** manifest-driven generator for agent-focused container images, Dockerfile overlays, and multi-service compose stacks. YAML definitions under `definitions/` drive deterministic output under `generated/`, with CI enforcing validation, regeneration, drift detection, security scanning, end-to-end container tests, and now **DocFX-backed documentation**.

## Why this repo exists

Agent CLIs all bring different runtime assumptions, auth models, shell expectations, volume needs, health semantics, and sidecar patterns. AgentContainers keeps those concerns explicit and composable:

- **Base images** define a primary runtime plus shared tools.
- **Combo images** combine multiple runtimes into one image family.
- **Agent overlays** layer provider CLIs onto compatible bases and combos.
- **Tool packs** add optional capabilities like developer utilities or sidecars.
- **Compose stacks** turn the image matrix into repeatable multi-container setups.

## Current image matrix

### Base runtimes

| ID | Purpose | Generated Dockerfile |
|---|---|---|
| `node-bun` | Node.js + Bun agent runtime | `generated/docker/bases/node-bun/Dockerfile` |
| `python` | Python runtime image | `generated/docker/bases/python/Dockerfile` |
| `dotnet` | .NET SDK runtime image | `generated/docker/bases/dotnet/Dockerfile` |
| `rust` | Rust toolchain runtime image | `generated/docker/bases/rust/Dockerfile` |

### Combo runtimes

| ID | Bases | Generated Dockerfile |
|---|---|---|
| `node-py-dotnet` | `node-bun` + `python` + `dotnet` | `generated/docker/combos/node-py-dotnet/Dockerfile` |
| `fullstack-polyglot` | `node-bun` + `rust` + `python` + `dotnet` | `generated/docker/combos/fullstack-polyglot/Dockerfile` |

### Agent overlays

| Agent | Supported runtimes | Generated Dockerfile pattern |
|---|---|---|
| `claude` | `node-bun`, `node-py-dotnet`, `fullstack-polyglot` | `generated/docker/agents/<runtime>-claude/Dockerfile` |
| `codex` | `node-bun`, `node-py-dotnet`, `fullstack-polyglot` | `generated/docker/agents/<runtime>-codex/Dockerfile` |
| `copilot` | `node-bun`, `node-py-dotnet`, `fullstack-polyglot` | `generated/docker/agents/<runtime>-copilot/Dockerfile` |
| `openclaw` | `node-bun`, `node-py-dotnet`, `fullstack-polyglot` | `generated/docker/agents/<runtime>-openclaw/Dockerfile` |

### Tool packs

| Tool pack | Mode | Compatible runtimes | Generated Dockerfile pattern |
|---|---|---|---|
| `devtools` | Overlay image | `node-bun`, `node-py-dotnet`, `fullstack-polyglot` | `generated/docker/tool-packs/<runtime>-devtools/Dockerfile` |
| `headroom` | Sidecar/service pack | Compose-driven | Sidecar wiring is generated through compose stacks |

### Generated compose stacks

| Stack | Scenario | Generated file |
|---|---|---|
| `solo-claude` | Single Claude Code container | `generated/compose/stacks/solo-claude/docker-compose.yaml` |
| `solo-codex` | Single Codex container | `generated/compose/stacks/solo-codex/docker-compose.yaml` |
| `solo-copilot` | Single Copilot container | `generated/compose/stacks/solo-copilot/docker-compose.yaml` |
| `gateway-headroom` | OpenClaw + Headroom + Claude topology | `generated/compose/stacks/gateway-headroom/docker-compose.yaml` |
| `polyglot-devtools` | Polyglot runtime with devtools overlay | `generated/compose/stacks/polyglot-devtools/docker-compose.yaml` |

## Quick start

```bash
# Restore, build, test
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build

# Validate manifests and regenerate checked-in artifacts
dotnet run --project src/AgentContainers.Generator --configuration Release -- validate
dotnet run --project src/AgentContainers.Generator --configuration Release -- generate

# Inspect the compatibility matrix
dotnet run --project src/AgentContainers.Generator --configuration Release -- list-matrix

# Build the documentation site
dotnet tool restore
dotnet tool run docfx docs/docfx.json --warningsAsErrors
```

## Dockerfile and compose conventions

- Every generated Dockerfile is committed under `generated/docker/` for transparency and pull-based use.
- Base, combo, agent, and tool-pack images all include OCI labels and deterministic build metadata.
- Overlay Dockerfiles accept a `BASE_IMAGE` build arg so local and published image flows use the same artifact.
- Compose stacks use manifest-driven env wiring, health-gated dependencies, optional profiles, and sidecar-aware environment injection.

## Documentation

The repo ships a DocFX site rooted at `docs/` with:

- **User and operator docs**: getting started, user guide, examples, compose guidance, environment contract
- **Generator/app docs**: CLI reference, extension workflow, Dockerfile generation model, matrix documentation
- **API reference**: generated from the .NET projects via DocFX metadata
- **Planning and architecture docs**: preserved under `docs/plans/`

Key entry points:

- `docs/index.md`
- `docs/articles/dockerfile-matrix.md`
- `docs/ENV-CONTRACT.md`
- `docs/e2e-testing.md`
- `docs/plans/README.md`

## Validation and workflows

The repository is wired so docs and generated artifacts are treated like product surface area:

- **CI** builds, tests, validates manifests, regenerates artifacts, checks drift, and builds the DocFX site.
- **Docs workflow** builds the site on PRs and publishes it from `main`.
- **E2E workflow** builds and validates generated images and a representative compose runtime path.
- **Publish workflow** builds and publishes the image matrix to GHCR.
- **Security workflow** runs Hadolint and Trivy against generated Dockerfiles and images.

## Local docs preview

```bash
dotnet tool restore
dotnet tool run docfx docs/docfx.json --serve
```

The generated site is written to `docs/_site/`.
