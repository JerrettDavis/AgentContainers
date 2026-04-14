# Contributing to AgentContainers

Thank you for your interest in contributing! This document covers the basics.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
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
| `docs/` | DocFX site content, guides, API/reference wiring |

## Current Corpus

### Agents (4)
| Agent | Description |
|---|---|
| `claude` | Anthropic Claude Code CLI |
| `codex` | OpenAI Codex CLI |
| `copilot` | GitHub Copilot CLI |
| `openclaw` | OpenClaw agent |

### Base Runtimes (4)
| Base | Description |
|---|---|
| `node-bun` | Node.js 22 LTS + Bun |
| `rust` | Rust stable toolchain |
| `python` | Python 3.12 + pip/venv |
| `dotnet` | .NET SDK runtime image |

### Combo Runtimes (2)
| Combo | Bases |
|---|---|
| `node-py-dotnet` | node-bun → python → dotnet |
| `fullstack-polyglot` | node-bun → rust → python → dotnet |

### Tool Packs (2)
| Pack | Description |
|---|---|
| `headroom` | Token-optimization proxy (sidecar) |
| `devtools` | Batteries-included developer power tools — linters, formatters, debuggers, build tools, productivity CLIs |

### Compose Stacks (5)
`solo-claude`, `solo-codex`, `solo-copilot`, `gateway-headroom`, `polyglot-devtools`

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

# Build the DocFX site
dotnet tool restore
dotnet tool run docfx docs/docfx.json --warningsAsErrors
```

## Making Changes

### Manifest changes (`definitions/`)

1. Edit or add YAML files under `definitions/`.
2. Run `dotnet run --project src/AgentContainers.Generator -- validate` to check.
3. Run `dotnet run --project src/AgentContainers.Generator -- generate` to regenerate.
4. Commit both the definition change **and** the regenerated output.

### Documentation changes (`docs/`, `README.md`)

1. Update the relevant user, operator, or architecture docs.
2. Run `dotnet tool restore`.
3. Run `dotnet tool run docfx docs/docfx.json --warningsAsErrors`.
4. If you changed manifests or generator behavior, regenerate artifacts and verify docs still match the matrix.

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
