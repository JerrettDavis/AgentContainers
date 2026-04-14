# Generated artifacts

This directory is **machine-owned** and committed to source control so users can
inspect the exact Dockerfiles, compose stacks, and catalogs produced by the
manifest-driven generator.

## What lives here

| Path | Purpose |
|---|---|
| `docker/bases/` | Base runtime Dockerfiles |
| `docker/combos/` | Combo runtime Dockerfiles |
| `docker/agents/` | Agent overlay Dockerfiles |
| `docker/tool-packs/` | Non-sidecar tool-pack overlay Dockerfiles |
| `compose/fragments/` | Generated compose fragments |
| `compose/stacks/` | Generated runnable compose stacks |
| `image-catalog.json` | Image matrix catalog for docs and tooling |
| `generation-report.json` | Deterministic artifact manifest and content hashes |

## Rules

- Do **not** edit files in this directory by hand.
- Regenerate with `scripts/generate.ps1`, `scripts/generate.sh`, or:

```bash
dotnet run --project src/AgentContainers.Generator --configuration Release -- generate
```

- Commit manifest or generator changes together with the updated generated output.
