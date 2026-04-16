# AgentContainers

AgentContainers is a **.NET 10** manifest-driven generator for agent-focused container images, Dockerfile overlays, and multi-service compose stacks. YAML definitions under `definitions/` drive deterministic output under `generated/`, with CI enforcing validation, regeneration, drift detection, security scanning, end-to-end container tests, and now **DocFX-backed documentation**.

## Why this repo exists

Agent CLIs all bring different runtime assumptions, auth models, shell expectations, volume needs, health semantics, and sidecar patterns. AgentContainers keeps those concerns explicit and composable:

- **Base images** define a primary runtime plus shared tools.
- **Combo images** combine multiple runtimes into one image family.
- **Agent overlays** layer provider CLIs onto compatible bases and combos.
- **Tool packs** add optional capabilities like developer utilities or sidecars.
- **Tag policies** define the curated public images, convenience repository names, and floating tag aliases.
- **Compose stacks** turn the image matrix into repeatable multi-container setups.

## Current image matrix

The repo still generates the full internal runtime and overlay matrix under `generated/docker/`, but public publishing is now driven by curated manifests in `definitions/tag-policies/`. That keeps ingredient generation transparent while letting GHCR expose a smaller set of convenience-oriented names and tags.

### Generated image directory

| Type | Image ID | Loadout | Generated Dockerfile |
|---|---|---|---|
| Base | `dotnet` | .NET 10 SDK runtime | `generated/docker/bases/dotnet/Dockerfile` |
| Base | `node-bun` | Node.js 24 + Bun runtime | `generated/docker/bases/node-bun/Dockerfile` |
| Base | `python` | Python 3.12 runtime | `generated/docker/bases/python/Dockerfile` |
| Base | `rust` | Rust toolchain runtime | `generated/docker/bases/rust/Dockerfile` |
| Combo | `node-py-dotnet` | `node-bun` + `python` + `dotnet` | `generated/docker/combos/node-py-dotnet/Dockerfile` |
| Combo | `fullstack-polyglot` | `node-bun` + `rust` + `python` + `dotnet` | `generated/docker/combos/fullstack-polyglot/Dockerfile` |
| Agent overlay | `node-bun-claude` | `node-bun` + `claude` | `generated/docker/agents/node-bun-claude/Dockerfile` |
| Agent overlay | `node-bun-codex` | `node-bun` + `codex` | `generated/docker/agents/node-bun-codex/Dockerfile` |
| Agent overlay | `node-bun-copilot` | `node-bun` + `copilot` | `generated/docker/agents/node-bun-copilot/Dockerfile` |
| Agent overlay | `node-bun-openclaw` | `node-bun` + `openclaw` | `generated/docker/agents/node-bun-openclaw/Dockerfile` |
| Agent overlay | `node-py-dotnet-claude` | `node-py-dotnet` + `claude` | `generated/docker/agents/node-py-dotnet-claude/Dockerfile` |
| Agent overlay | `node-py-dotnet-codex` | `node-py-dotnet` + `codex` | `generated/docker/agents/node-py-dotnet-codex/Dockerfile` |
| Agent overlay | `node-py-dotnet-copilot` | `node-py-dotnet` + `copilot` | `generated/docker/agents/node-py-dotnet-copilot/Dockerfile` |
| Agent overlay | `node-py-dotnet-openclaw` | `node-py-dotnet` + `openclaw` | `generated/docker/agents/node-py-dotnet-openclaw/Dockerfile` |
| Agent overlay | `fullstack-polyglot-claude` | `fullstack-polyglot` + `claude` | `generated/docker/agents/fullstack-polyglot-claude/Dockerfile` |
| Agent overlay | `fullstack-polyglot-codex` | `fullstack-polyglot` + `codex` | `generated/docker/agents/fullstack-polyglot-codex/Dockerfile` |
| Agent overlay | `fullstack-polyglot-copilot` | `fullstack-polyglot` + `copilot` | `generated/docker/agents/fullstack-polyglot-copilot/Dockerfile` |
| Agent overlay | `fullstack-polyglot-openclaw` | `fullstack-polyglot` + `openclaw` | `generated/docker/agents/fullstack-polyglot-openclaw/Dockerfile` |
| Tool-pack overlay | `node-bun-devtools` | `node-bun` + `devtools` | `generated/docker/tool-packs/node-bun-devtools/Dockerfile` |
| Tool-pack overlay | `node-py-dotnet-devtools` | `node-py-dotnet` + `devtools` | `generated/docker/tool-packs/node-py-dotnet-devtools/Dockerfile` |
| Tool-pack overlay | `fullstack-polyglot-devtools` | `fullstack-polyglot` + `devtools` | `generated/docker/tool-packs/fullstack-polyglot-devtools/Dockerfile` |
| Curated publish target | `dotnet-claude` | `node-py-dotnet` + `claude` + `devtools` | `generated/docker/images/dotnet-claude/Dockerfile` |
| Curated publish target | `dotnet-codex` | `node-py-dotnet` + `codex` + `devtools` | `generated/docker/images/dotnet-codex/Dockerfile` |
| Curated publish target | `dotnet-copilot` | `node-py-dotnet` + `copilot` + `devtools` | `generated/docker/images/dotnet-copilot/Dockerfile` |
| Curated publish target | `openclaw-dotnet` | `node-py-dotnet` + `openclaw` + `devtools` | `generated/docker/images/openclaw-dotnet/Dockerfile` |
| Curated publish target | `polyglot-menagerie` | `fullstack-polyglot` + `claude` + `codex` + `copilot` + `openclaw` + `devtools` | `generated/docker/images/polyglot-menagerie/Dockerfile` |
| Curated publish target | `tools-swiss-army` | `fullstack-polyglot` + `devtools` | `generated/docker/images/tools-swiss-army/Dockerfile` |

`headroom` remains a sidecar/service pack and is published through generated compose stacks rather than a standalone Dockerfile image.

### Published runtime ingredient repos

These are the six runtime repos the workflow still publishes as build ingredients for overlays and curated images:

| Repo | Backing image | Tag channels |
|---|---|---|
| `ghcr.io/<owner>/dotnet` | `dotnet` | `<version>`, `<major>.<minor>`, `sha-<commit>`, `<branch>`, `latest` on release tags |
| `ghcr.io/<owner>/node-bun` | `node-bun` | `<version>`, `<major>.<minor>`, `sha-<commit>`, `<branch>`, `latest` on release tags |
| `ghcr.io/<owner>/python` | `python` | `<version>`, `<major>.<minor>`, `sha-<commit>`, `<branch>`, `latest` on release tags |
| `ghcr.io/<owner>/rust` | `rust` | `<version>`, `<major>.<minor>`, `sha-<commit>`, `<branch>`, `latest` on release tags |
| `ghcr.io/<owner>/node-py-dotnet` | `node-py-dotnet` | `<version>`, `<major>.<minor>`, `sha-<commit>`, `<branch>`, `latest` on release tags |
| `ghcr.io/<owner>/fullstack-polyglot` | `fullstack-polyglot` | `<version>`, `<major>.<minor>`, `sha-<commit>`, `<branch>`, `latest` on release tags |

### Curated public tag directory

The curated publish matrix is generator-owned. Floating major and minor aliases resolve to the newest declared `release_version` in that range.

| Publish target | Primary tag | All published tags |
|---|---|---|
| `dotnet-claude` | `ghcr.io/<owner>/dotnet:claude-0.1.0` | `ghcr.io/<owner>/dotnet:claude-0.1.0`<br>`ghcr.io/<owner>/dotnet:claude-0.1`<br>`ghcr.io/<owner>/dotnet:claude-0`<br>`ghcr.io/<owner>/dotnet:claude-latest`<br>`ghcr.io/<owner>/claude:dotnet10-node24-0.1.0`<br>`ghcr.io/<owner>/claude:dotnet10-node24` |
| `dotnet-codex` | `ghcr.io/<owner>/dotnet:codex-0.1.0` | `ghcr.io/<owner>/dotnet:codex-0.1.0`<br>`ghcr.io/<owner>/dotnet:codex-0.1`<br>`ghcr.io/<owner>/dotnet:codex-0`<br>`ghcr.io/<owner>/dotnet:codex-latest`<br>`ghcr.io/<owner>/codex:dotnet10-node24-0.1.0`<br>`ghcr.io/<owner>/codex:dotnet10-node24` |
| `dotnet-copilot` | `ghcr.io/<owner>/dotnet:copilot-0.1.0` | `ghcr.io/<owner>/dotnet:copilot-0.1.0`<br>`ghcr.io/<owner>/dotnet:copilot-0.1`<br>`ghcr.io/<owner>/dotnet:copilot-0`<br>`ghcr.io/<owner>/dotnet:copilot-latest`<br>`ghcr.io/<owner>/copilot:dotnet10-node24-0.1.0`<br>`ghcr.io/<owner>/copilot:dotnet10-node24` |
| `openclaw-dotnet` | `ghcr.io/<owner>/dotnet:openclaw-0.1.0` | `ghcr.io/<owner>/dotnet:openclaw-0.1.0`<br>`ghcr.io/<owner>/dotnet:openclaw-0.1`<br>`ghcr.io/<owner>/dotnet:openclaw-0`<br>`ghcr.io/<owner>/dotnet:openclaw-latest`<br>`ghcr.io/<owner>/openclaw:dotnet10-node24-devtools-0.1.0`<br>`ghcr.io/<owner>/openclaw:dotnet10-node24-devtools` |
| `polyglot-menagerie` | `ghcr.io/<owner>/polyglot:menagerie-0.1.0` | `ghcr.io/<owner>/polyglot:menagerie-0.1.0`<br>`ghcr.io/<owner>/polyglot:menagerie`<br>`ghcr.io/<owner>/menagerie:dotnet10-node24-python312-rust-0.1.0`<br>`ghcr.io/<owner>/menagerie:dotnet10-node24-python312-rust-latest` |
| `tools-swiss-army` | `ghcr.io/<owner>/tools:swiss-army-0.1.0` | `ghcr.io/<owner>/tools:swiss-army-0.1.0`<br>`ghcr.io/<owner>/tools:swiss-army`<br>`ghcr.io/<owner>/polyglot:toolbox-0.1.0`<br>`ghcr.io/<owner>/polyglot:toolbox` |

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
- Base, combo, agent, tool-pack, and curated publish-target images all include OCI labels and deterministic build metadata.
- Overlay Dockerfiles accept a `BASE_IMAGE` build arg so local and published image flows use the same artifact.
- Curated publish-target Dockerfiles also accept `BASE_IMAGE`, so one resolved loadout can be published under multiple repository/tag aliases without duplicating build logic.
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
- **Publish workflow** builds the base/combo ingredients and then publishes only the curated tag-policy image set to GHCR.
- **Security workflow** runs Hadolint and Trivy against generated Dockerfiles and images.

## Local docs preview

```bash
dotnet tool restore
dotnet tool run docfx docs/docfx.json --serve
```

The generated site is written to `docs/_site/`.
