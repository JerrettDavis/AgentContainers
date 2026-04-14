# User guide

## Choosing an image

Use the image families based on the runtime surface you need:

- **Base images** when you want a curated runtime with common tools
- **Combo images** when one container needs multiple languages
- **Agent overlays** when you want an agent CLI preinstalled
- **Tool-pack overlays** when you want extra utilities like `devtools`
- **Compose stacks** when you want a ready-to-run multi-container topology

## Consuming generated Dockerfiles

Every image type has a checked-in Dockerfile under `generated/docker/`.

```bash
docker build -t agentcontainers/node-bun:latest generated/docker/bases/node-bun
docker build -t agentcontainers/node-bun-claude:latest generated/docker/agents/node-bun-claude
docker build -t agentcontainers/node-bun-devtools:latest generated/docker/tool-packs/node-bun-devtools
```

Agent and tool-pack overlays accept a `BASE_IMAGE` build arg so local builds and published-image builds use the same artifact:

```bash
docker build \
  --build-arg BASE_IMAGE=ghcr.io/agentcontainers/node-bun:latest \
  -t ghcr.io/agentcontainers/node-bun-claude:latest \
  generated/docker/agents/node-bun-claude
```

## Running compose stacks

Generated compose files live under `generated/compose/stacks/`.

```bash
docker compose -f generated/compose/stacks/solo-claude/docker-compose.yaml up
docker compose -f generated/compose/stacks/gateway-headroom/docker-compose.yaml up
```

Use a `.env` file when the stack declares `env_file`, or export required secrets before starting a service:

```bash
export ANTHROPIC_API_KEY=...
docker compose -f generated/compose/stacks/solo-claude/docker-compose.yaml up
```

## Regenerating after manifest changes

1. Edit manifests under `definitions/`.
2. Run `validate`.
3. Run `generate`.
4. Review changes in `generated/`.
5. Re-run the relevant e2e or compose validation path.

## Where to go deeper

- [Generator CLI reference](generator-cli.md)
- [Dockerfile matrix](dockerfile-matrix.md)
- [Environment variable contract](../ENV-CONTRACT.md)
- [End-to-end validation](../e2e-testing.md)
