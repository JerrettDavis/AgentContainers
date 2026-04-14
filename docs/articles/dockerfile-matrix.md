# Dockerfile matrix

This page is the operator-facing index of the checked-in Dockerfile and compose matrix.

## Base Dockerfiles

| Image ID | Upstream family | Key capabilities | Dockerfile |
|---|---|---|---|
| `dotnet` | .NET SDK | `dotnet`, `csharp`, `fsharp` | `generated/docker/bases/dotnet/Dockerfile` |
| `node-bun` | Node.js + Bun | `node`, `npm`, `bun`, `javascript` | `generated/docker/bases/node-bun/Dockerfile` |
| `python` | Python | `python`, `pip`, `venv` | `generated/docker/bases/python/Dockerfile` |
| `rust` | Rust | `rust`, `cargo`, `rustup` | `generated/docker/bases/rust/Dockerfile` |

## Combo Dockerfiles

| Image ID | Runtime chain | Dockerfile |
|---|---|---|
| `node-py-dotnet` | `node-bun` -> `python` -> `dotnet` | `generated/docker/combos/node-py-dotnet/Dockerfile` |
| `fullstack-polyglot` | `node-bun` -> `rust` -> `python` -> `dotnet` | `generated/docker/combos/fullstack-polyglot/Dockerfile` |

## Agent overlay Dockerfiles

| Runtime | Claude | Codex | Copilot | OpenClaw |
|---|---|---|---|---|
| `node-bun` | `generated/docker/agents/node-bun-claude/Dockerfile` | `generated/docker/agents/node-bun-codex/Dockerfile` | `generated/docker/agents/node-bun-copilot/Dockerfile` | `generated/docker/agents/node-bun-openclaw/Dockerfile` |
| `node-py-dotnet` | `generated/docker/agents/node-py-dotnet-claude/Dockerfile` | `generated/docker/agents/node-py-dotnet-codex/Dockerfile` | `generated/docker/agents/node-py-dotnet-copilot/Dockerfile` | `generated/docker/agents/node-py-dotnet-openclaw/Dockerfile` |
| `fullstack-polyglot` | `generated/docker/agents/fullstack-polyglot-claude/Dockerfile` | `generated/docker/agents/fullstack-polyglot-codex/Dockerfile` | `generated/docker/agents/fullstack-polyglot-copilot/Dockerfile` | `generated/docker/agents/fullstack-polyglot-openclaw/Dockerfile` |

## Tool-pack overlay Dockerfiles

| Runtime | Devtools overlay |
|---|---|
| `node-bun` | `generated/docker/tool-packs/node-bun-devtools/Dockerfile` |
| `node-py-dotnet` | `generated/docker/tool-packs/node-py-dotnet-devtools/Dockerfile` |
| `fullstack-polyglot` | `generated/docker/tool-packs/fullstack-polyglot-devtools/Dockerfile` |

## Compose matrix

| Stack | Main surfaces | Compose file |
|---|---|---|
| `solo-claude` | `node-bun-claude` | `generated/compose/stacks/solo-claude/docker-compose.yaml` |
| `solo-codex` | `node-bun-codex` | `generated/compose/stacks/solo-codex/docker-compose.yaml` |
| `solo-copilot` | `node-bun-copilot` | `generated/compose/stacks/solo-copilot/docker-compose.yaml` |
| `gateway-headroom` | `node-bun-openclaw`, `node-bun-claude`, `headroom` sidecar | `generated/compose/stacks/gateway-headroom/docker-compose.yaml` |
| `polyglot-devtools` | `fullstack-polyglot-devtools` | `generated/compose/stacks/polyglot-devtools/docker-compose.yaml` |

## Validation status

The current matrix is covered by the full e2e suite:

- all 4 base images
- both combo images
- all 12 agent overlays
- all 3 tool-pack overlays
- all generated compose configurations
- runtime validation of the representative generated compose path
