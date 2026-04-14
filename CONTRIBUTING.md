# Contributing to AgentContainers

Thank you for your interest in contributing! This document covers the basics.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git

## Repository Layout

| Path | Purpose |
|---|---|
| `definitions/` | YAML manifest sources (agents, bases, combos, tool-packs, …) |
| `schemas/` | JSON Schema files for manifest validation |
| `templates/` | Scriban templates consumed by the generator |
| `src/AgentContainers.Core/` | Shared library: models, loading, validation |
| `src/AgentContainers.Generator/` | CLI tool: validate, generate, list-matrix, build-matrix |
| `tests/AgentContainers.Tests/` | Unit & integration tests |
| `generated/` | **Auto-generated** — do not edit by hand |
| `scripts/` | Helper scripts for local dev |

## Quick Start

```bash
# Build everything
dotnet build

# Run tests
dotnet test

# Validate manifests
dotnet run --project src/AgentContainers.Generator -- validate

# Regenerate artifacts
dotnet run --project src/AgentContainers.Generator -- generate

# Print compatibility matrix
dotnet run --project src/AgentContainers.Generator -- list-matrix

# Emit build matrix for CI/CD publishing
dotnet run --project src/AgentContainers.Generator -- build-matrix
```

## Making Changes

### Manifest changes (`definitions/`)

1. Edit or add YAML files under `definitions/`.
2. Run `dotnet run --project src/AgentContainers.Generator -- validate` to check.
3. Run `dotnet run --project src/AgentContainers.Generator -- generate` to regenerate.
4. Commit both the definition change **and** the regenerated output.

### Code changes (`src/`)

1. Make your changes.
2. Run `dotnet build` — the solution has `TreatWarningsAsErrors` enabled.
3. Run `dotnet test` — all tests must pass.
4. If your change affects generation output, regenerate and commit the diff.

## Drift Detection

CI verifies that the checked-in `generated/` directory matches a fresh
`generate` run. If your PR shows drift, regenerate locally and commit.

## Coding Standards

- Follow the `.editorconfig` settings.
- Use file-scoped namespaces.
- Keep model classes in `AgentContainers.Core.Models`.
- Keep validation logic in `AgentContainers.Core.Validation`.

## Pull Requests

- Keep PRs focused — one logical change per PR.
- Include a clear description of *why* the change is needed.
- CI must be green before merge.
