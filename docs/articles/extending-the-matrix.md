# Extending the matrix

## Add a new base runtime

1. Add a YAML manifest under `definitions/bases/`.
2. Declare the upstream image, capabilities, install steps, env defaults, and validation commands.
3. Run:

```bash
dotnet run --project src/AgentContainers.Generator --configuration Release -- validate
dotnet run --project src/AgentContainers.Generator --configuration Release -- generate
```

4. Review the new Dockerfile and image catalog entries under `generated/`.
5. Run targeted e2e validation.

## Add a new agent overlay

1. Add a manifest under `definitions/agents/`.
2. Model runtime requirements with `requires`.
3. Add CLI install metadata, env vars, mounts, health checks, and validation commands.
4. Regenerate artifacts and verify the new overlay Dockerfiles under `generated/docker/agents/`.

## Add a new tool pack

Use `definitions/tool-packs/` for either:

- **overlay packs** that emit Dockerfiles, or
- **sidecar packs** that inject compose services and environment contracts

When the pack is sidecar-oriented, document its environment contract and health behavior so compose examples stay operable.

## Add or update a compose stack

1. Add a stack manifest under `definitions/compose/`.
2. Reference bases, combos, agents, ports, networks, volumes, tool packs, and `env_file` where needed.
3. Regenerate the stack output under `generated/compose/stacks/`.

## Keep docs and workflows aligned

When you change the matrix:

1. Regenerate artifacts.
2. Update any user-facing docs that describe the supported surface.
3. Run DocFX locally.
4. Run the appropriate e2e scope.

The CI and docs workflows will re-check those surfaces automatically.
