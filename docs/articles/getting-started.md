# Getting started

## Prerequisites

- .NET 10 SDK
- Docker Engine or Docker Desktop
- Git
- Internet access for upstream base images and package installs

## Common local workflow

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
dotnet run --project src/AgentContainers.Generator --configuration Release -- validate
dotnet run --project src/AgentContainers.Generator --configuration Release -- generate
```

## Key directories

| Path | Purpose |
|---|---|
| `definitions/` | Source-of-truth YAML manifests |
| `src/` | Generator and shared manifest/model code |
| `generated/` | Committed Dockerfiles, compose stacks, and catalogs |
| `docs/` | DocFX site content, guides, and architecture docs |
| `schemas/` | JSON Schema files used by validation |
| `scripts/` | Helper scripts for generation, docs, and e2e runs |

## Build the docs site

```bash
dotnet tool restore
dotnet tool run docfx docs/docfx.json --warningsAsErrors
```

To preview the site locally:

```bash
dotnet tool run docfx docs/docfx.json --serve
```

## Validate the container surface

```bash
# Quick scope
./scripts/run-e2e.sh

# Full scope
./scripts/run-e2e.sh full --no-cleanup
```

On Windows:

```powershell
.\scripts\run-e2e.ps1 -Scope full -NoCleanup
```
