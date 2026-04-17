# Dockerfile matrix

This page is the operator-facing index of the checked-in Dockerfile and compose matrix.

## Base Dockerfiles

| Image ID | Upstream family | Key capabilities | Dockerfile |
|---|---|---|---|
| `dotnet` | .NET SDK | `dotnet`, `csharp`, `fsharp` | `generated/docker/bases/dotnet/Dockerfile` |
| `node-bun` | Node.js + Bun | `node`, `npm`, `bun`, `javascript` | `generated/docker/bases/node-bun/Dockerfile` |
| `python` | Python | `python`, `pip`, `venv` | `generated/docker/bases/python/Dockerfile` |
| `rust` | Rust | `rust`, `cargo`, `rustup` | `generated/docker/bases/rust/Dockerfile` |

### Base image `docker run` batch

```bash
docker run --rm -it ghcr.io/JerrettDavis/ac-dotnet:0.1.0 dotnet --info
docker run --rm -it ghcr.io/JerrettDavis/ac-node-bun:0.1 node --version
docker run --rm -it ghcr.io/JerrettDavis/ac-python:latest python3 --version
docker run --rm -it ghcr.io/JerrettDavis/ac-rust:main rustc --version
```

## Combo Dockerfiles

| Image ID | Runtime chain | Dockerfile |
|---|---|---|
| `node-py-dotnet` | `node-bun` -> `python` -> `dotnet` | `generated/docker/combos/node-py-dotnet/Dockerfile` |
| `fullstack-polyglot` | `node-bun` -> `rust` -> `python` -> `dotnet` | `generated/docker/combos/fullstack-polyglot/Dockerfile` |

### Combo image `docker run` batch

```bash
docker run --rm -it ghcr.io/JerrettDavis/ac-node-py-dotnet:latest bash -lc "node --version && python3 --version && dotnet --version"
docker run --rm -it ghcr.io/JerrettDavis/ac-fullstack-polyglot:main bash -lc "node --version && rustc --version && python3 --version && dotnet --version"
```

## Agent overlay Dockerfiles

| Runtime | Claude | Codex | Copilot | OpenClaw |
|---|---|---|---|---|
| `node-bun` | `generated/docker/agents/node-bun-claude/Dockerfile` | `generated/docker/agents/node-bun-codex/Dockerfile` | `generated/docker/agents/node-bun-copilot/Dockerfile` | `generated/docker/agents/node-bun-openclaw/Dockerfile` |
| `node-py-dotnet` | `generated/docker/agents/node-py-dotnet-claude/Dockerfile` | `generated/docker/agents/node-py-dotnet-codex/Dockerfile` | `generated/docker/agents/node-py-dotnet-copilot/Dockerfile` | `generated/docker/agents/node-py-dotnet-openclaw/Dockerfile` |
| `fullstack-polyglot` | `generated/docker/agents/fullstack-polyglot-claude/Dockerfile` | `generated/docker/agents/fullstack-polyglot-codex/Dockerfile` | `generated/docker/agents/fullstack-polyglot-copilot/Dockerfile` | `generated/docker/agents/fullstack-polyglot-openclaw/Dockerfile` |

These overlays are generated and testable locally, but they are not the curated public GHCR surface anymore.

### Agent overlay local demo batch

```bash
docker build -t local/node-bun-claude generated/docker/agents/node-bun-claude
docker run --rm -it -e ANTHROPIC_API_KEY=your-key -v "$PWD:/workspace" local/node-bun-claude claude --version

docker build -t local/node-py-dotnet-codex generated/docker/agents/node-py-dotnet-codex
docker run --rm -it -e OPENAI_API_KEY=your-key -v "$PWD:/workspace" local/node-py-dotnet-codex codex --version

docker build -t local/fullstack-polyglot-openclaw generated/docker/agents/fullstack-polyglot-openclaw
docker run --rm -it -e OPENCLAW_API_KEY=your-key -p 3000:3000 local/fullstack-polyglot-openclaw
```

## Tool-pack overlay Dockerfiles

| Runtime | Devtools overlay |
|---|---|
| `node-bun` | `generated/docker/tool-packs/node-bun-devtools/Dockerfile` |
| `node-py-dotnet` | `generated/docker/tool-packs/node-py-dotnet-devtools/Dockerfile` |
| `fullstack-polyglot` | `generated/docker/tool-packs/fullstack-polyglot-devtools/Dockerfile` |

### Tool-pack local demo batch

```bash
docker build -t local/node-bun-devtools generated/docker/tool-packs/node-bun-devtools
docker run --rm -it -v "$PWD:/workspace" local/node-bun-devtools bash -lc "prettier --version && ruff --version"

docker build -t local/fullstack-polyglot-devtools generated/docker/tool-packs/fullstack-polyglot-devtools
docker run --rm -it -v "$PWD:/workspace" local/fullstack-polyglot-devtools bash -lc "shellcheck --version && black --version"
```

## Curated public image batches

| Publish target | Primary public tag | Alternate public tags |
|---|---|---|
| `dotnet-claude` | `ghcr.io/JerrettDavis/ac-dotnet:claude-0.1.0` | `ghcr.io/JerrettDavis/ac-dotnet:claude-0.1`, `ghcr.io/JerrettDavis/ac-dotnet:claude-0`, `ghcr.io/JerrettDavis/ac-dotnet:claude-latest`, `ghcr.io/JerrettDavis/ac-claude:dotnet10-node24` |
| `dotnet-codex` | `ghcr.io/JerrettDavis/ac-dotnet:codex-0.1.0` | `ghcr.io/JerrettDavis/ac-dotnet:codex-0.1`, `ghcr.io/JerrettDavis/ac-dotnet:codex-0`, `ghcr.io/JerrettDavis/ac-dotnet:codex-latest`, `ghcr.io/JerrettDavis/ac-codex:dotnet10-node24` |
| `dotnet-copilot` | `ghcr.io/JerrettDavis/ac-dotnet:copilot-0.1.0` | `ghcr.io/JerrettDavis/ac-dotnet:copilot-0.1`, `ghcr.io/JerrettDavis/ac-dotnet:copilot-0`, `ghcr.io/JerrettDavis/ac-dotnet:copilot-latest`, `ghcr.io/JerrettDavis/ac-copilot:dotnet10-node24` |
| `openclaw-dotnet` | `ghcr.io/JerrettDavis/ac-dotnet:openclaw-0.1.0` | `ghcr.io/JerrettDavis/ac-dotnet:openclaw-0.1`, `ghcr.io/JerrettDavis/ac-dotnet:openclaw-0`, `ghcr.io/JerrettDavis/ac-dotnet:openclaw-latest`, `ghcr.io/JerrettDavis/ac-openclaw:dotnet10-node24-devtools` |
| `polyglot-menagerie` | `ghcr.io/JerrettDavis/ac-polyglot:menagerie-0.1.0` | `ghcr.io/JerrettDavis/ac-polyglot:menagerie`, `ghcr.io/JerrettDavis/ac-menagerie:dotnet10-node24-python312-rust-latest` |
| `tools-swiss-army` | `ghcr.io/JerrettDavis/ac-tools:swiss-army-0.1.0` | `ghcr.io/JerrettDavis/ac-tools:swiss-army`, `ghcr.io/JerrettDavis/ac-polyglot:toolbox` |

### Curated public `docker run` batch

```bash
docker run --rm -it -e ANTHROPIC_API_KEY=your-key -v "$PWD:/workspace" ghcr.io/JerrettDavis/ac-dotnet:claude-0.1.0 claude --version
docker run --rm -it -e OPENAI_API_KEY=your-key -v "$PWD:/workspace" ghcr.io/JerrettDavis/ac-codex:dotnet10-node24 codex --version
docker run --rm -it -e GITHUB_TOKEN=your-token -v "$PWD:/workspace" ghcr.io/JerrettDavis/ac-dotnet:copilot-latest github-copilot-cli --version
docker run --rm -it -e OPENCLAW_API_KEY=your-key -p 3000:3000 ghcr.io/JerrettDavis/ac-openclaw:dotnet10-node24-devtools
docker run --rm -it -v "$PWD:/workspace" ghcr.io/JerrettDavis/ac-polyglot:menagerie bash -lc "claude --version && codex --version && github-copilot-cli --version && openclaw --version"
docker run --rm -it -v "$PWD:/workspace" ghcr.io/JerrettDavis/ac-tools:swiss-army black --version
```

## Compose matrix

| Stack | Main surfaces | Compose file |
|---|---|---|
| `solo-claude` | `node-bun-claude` | `generated/compose/stacks/solo-claude/docker-compose.yaml` |
| `solo-codex` | `node-bun-codex` | `generated/compose/stacks/solo-codex/docker-compose.yaml` |
| `solo-copilot` | `node-bun-copilot` | `generated/compose/stacks/solo-copilot/docker-compose.yaml` |
| `gateway-headroom` | `node-bun-openclaw`, `node-bun-claude`, `headroom` sidecar | `generated/compose/stacks/gateway-headroom/docker-compose.yaml` |
| `polyglot-devtools` | `fullstack-polyglot-devtools` | `generated/compose/stacks/polyglot-devtools/docker-compose.yaml` |

### Compose demo batch

```bash
docker compose -f generated/compose/stacks/solo-claude/docker-compose.yaml up
docker compose -f generated/compose/stacks/solo-copilot/docker-compose.yaml up
docker compose -f generated/compose/stacks/gateway-headroom/docker-compose.yaml up
docker compose -f generated/compose/stacks/polyglot-devtools/docker-compose.yaml up
```

## Validation status

The current matrix is covered by the full e2e suite:

- all 4 base images
- both combo images
- all 12 agent overlays
- all 3 tool-pack overlays
- all generated compose configurations
- runtime validation of the representative generated compose path
