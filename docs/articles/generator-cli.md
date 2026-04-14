# Generator CLI reference

The generator entry point lives in `src/AgentContainers.Generator` and is typically invoked with:

```bash
dotnet run --project src/AgentContainers.Generator --configuration Release -- <command>
```

## Commands

| Command | Purpose | Output |
|---|---|---|
| `generate` | Validate manifests, load schemas, emit Dockerfiles, compose stacks, and catalog/report artifacts | Writes to `generated/` |
| `validate` | Load manifests and run catalog validation rules | Exit code only |
| `list-matrix` | Print the compatibility matrix in text form | Console table |
| `list-matrix --json` | Emit machine-readable matrix and valid combinations | JSON |
| `build-matrix` | Emit the publishing matrix for GitHub Actions | JSON |
| `emit-e2e-plan` | Emit the manifest-driven e2e plan consumed by the test runners | JSON |

## Typical usage

```bash
dotnet run --project src/AgentContainers.Generator --configuration Release -- validate
dotnet run --project src/AgentContainers.Generator --configuration Release -- generate
dotnet run --project src/AgentContainers.Generator --configuration Release -- list-matrix
dotnet run --project src/AgentContainers.Generator --configuration Release -- build-matrix
dotnet run --project src/AgentContainers.Generator --configuration Release -- emit-e2e-plan
```

## Build and publish flow

1. `validate` confirms catalog integrity.
2. `generate` refreshes checked-in artifacts.
3. `build-matrix` drives the publish workflow matrix.
4. `emit-e2e-plan` drives the e2e scripts and workflow.

## Related APIs

The main conceptual types behind the CLI are documented in the API reference:

- `AgentContainers.Core.Loading.ManifestLoader`
- `AgentContainers.Core.Validation.CatalogValidator`
- `AgentContainers.Core.Matrix.MatrixBuilder`
- `AgentContainers.Core.Hashing.ContentHasher`
