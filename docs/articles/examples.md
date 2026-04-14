# Examples

## Build a base image

```bash
docker build -t agentcontainers/node-bun:latest generated/docker/bases/node-bun
docker run --rm agentcontainers/node-bun:latest node --version
```

## Build an agent overlay from a local base

```bash
docker build -t agentcontainers/node-bun-claude:latest generated/docker/agents/node-bun-claude
docker run --rm agentcontainers/node-bun-claude:latest claude --version
```

## Build an agent overlay from a published base image

```bash
docker build \
  --build-arg BASE_IMAGE=ghcr.io/agentcontainers/node-bun:latest \
  -t ghcr.io/agentcontainers/node-bun-codex:latest \
  generated/docker/agents/node-bun-codex
```

## Run a compose stack

```bash
export ANTHROPIC_API_KEY=...
docker compose -f generated/compose/stacks/solo-claude/docker-compose.yaml up
```

## Run a richer stack with Headroom

```bash
export ANTHROPIC_API_KEY=...
docker compose -f generated/compose/stacks/gateway-headroom/docker-compose.yaml up
```

## Generate and inspect the matrix

```bash
dotnet run --project src/AgentContainers.Generator --configuration Release -- list-matrix
dotnet run --project src/AgentContainers.Generator --configuration Release -- list-matrix --json
```

## Build the docs site

```bash
dotnet tool restore
dotnet tool run docfx docs/docfx.json --warningsAsErrors
```
