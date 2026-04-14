# Dockerfiles and generated artifacts

## What is generated

The generator emits four Dockerfile families plus compose artifacts:

| Family | Path | Purpose |
|---|---|---|
| Base images | `generated/docker/bases/<base>/Dockerfile` | Primary runtime + common tool surface |
| Combo images | `generated/docker/combos/<combo>/Dockerfile` | Ordered multi-runtime unions |
| Agent overlays | `generated/docker/agents/<runtime>-<agent>/Dockerfile` | Agent CLI on top of a compatible runtime |
| Tool-pack overlays | `generated/docker/tool-packs/<runtime>-<pack>/Dockerfile` | Optional tool surface layered above a runtime |
| Compose stacks | `generated/compose/stacks/<stack>/docker-compose.yaml` | Runnable multi-service topologies |

## Design rules

- Generated files are **committed** for auditability.
- Source-of-truth manifests live under `definitions/`.
- Dockerfiles carry **OCI labels** and build metadata.
- Overlay images use `BASE_IMAGE` to support both local and registry-based builds.
- Environment defaults use explicit `ARG -> ENV` or compose override patterns.

## Common Dockerfile usage

```bash
# Base
docker build -t agentcontainers/python:latest generated/docker/bases/python

# Combo
docker build -t agentcontainers/node-py-dotnet:latest generated/docker/combos/node-py-dotnet

# Agent overlay
docker build -t agentcontainers/node-bun-codex:latest generated/docker/agents/node-bun-codex

# Tool-pack overlay
docker build -t agentcontainers/fullstack-polyglot-devtools:latest generated/docker/tool-packs/fullstack-polyglot-devtools
```

## Compose usage

```bash
docker compose -f generated/compose/stacks/solo-codex/docker-compose.yaml up
docker compose -f generated/compose/stacks/polyglot-devtools/docker-compose.yaml up
```

## Validation expectations

Generated artifacts are covered by:

- manifest validation
- drift detection in CI
- security scanning on generated Dockerfiles
- e2e container validation on built images and compose output
