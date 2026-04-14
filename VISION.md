# VISION.md

> **Planning home:** Concrete architectural decisions, implementation phases, manifest schema design, and Compose strategy are now maintained in [`docs/plans/`](docs/plans/README.md). This file remains the product vision and design overview.

## AgentContainers

AgentContainers is a repository for building, publishing, and composing container images for agentic CLI environments.

The core idea is simple.

Instead of hand-authoring and hand-maintaining a growing pile of one-off Dockerfiles for every agent, language stack, an([code.claude.com](https://code.claude.com/docs/en/devcontainer))ix-driven system that can generate those combinations consistently. The repository acts as both a source of truth and a distribution mechanism.

From a small, declarative set of configuration files, the system should be able to:

* define common tool layers used across all images
* define language and platform base images
* define multi-language combo images
* define agent provider overlays
* define optional tool-pack overlays
* generate static Dockerfiles into the repository for transparency and pull-based consumption
* build and publish tagged images through CI
* produce Docker Compose examples for real multi-agent topologies
* provide extension points so users can add new agents, runtimes, and tool packs without restructuring the repository

The long-term goal is to make agent container environments feel as composable and reliable as infrastructure modules. A user should be able to say, “I want Claude Code on a Node and Python image with Git, jq, GitHub CLI, and my own internal tools,” and the repo should already have a predictable way to express, generate, build, and run that.

## Why this repo should exist

Today, agent tooling is fragmented.

Different agents have different install methods, runtime assumptions, environment variable expectations, volumes, auth flows, persistence models, shell expectations, and network concerns. Some work best in devcontainers. Some prefer Docker. Some require Node. Some are Python-first. Some can be layered into general-purpose environments. Some need plugins, hooks, or gateway sidecars to become fully useful.

Without a unifying repo, teams end up with:

* snowflake Dockerfiles
* duplicated install logic
* inconsistent tags and naming
* drift between docs and actual images
* hard-to-reason-about Compose setups
* ad hoc observability and health checks
* brittle onboarding for developers and operators

AgentContainers solves that by standardizing how images are described, generated, validated, published, and composed.

## Product vision

AgentContainers should become a strong foundation for three kinds of users.

### 1. Individual developers

Developers should be able to quickly pull a ready-made image and start using an agent in a familiar shell with the tools they expect.

### 2. Teams and platform engineers

Teams should be able to maintain approved image families with well-defined tags, reproducible build pipelines, security scanning, policy enforcement, and observability.

### 3. Experimenters and orchestrators

People running multiple agent ecosystems at once should be able to launch richer topologies, such as:

* Claude Code in one container
* OpenClaw in another
* Headroom as a shared service or integrated proxy
* a Discord plugin sidecar or bot-connected runtime
* shared workspaces, persistent config volumes, and logs

## Principles

### Declarative first

The repo should prefer small declarative descriptors over hand-maintained imperative duplication.

### Generated, but inspectable

Everything generated should also be emitted as static files in the repository so users can inspect, diff, audit, and fork them.

### Composable layering

Common tools, runtime bases, combo stacks, agent overlays, and tool packs should be independently composable.

### Strong defaults, deep customization

A newcomer should be able to use the defaults immediately. An advanced user should be able to override nearly every meaningful behavior.

### Operationally honest

Images should expose health semantics, expected mounts, persistence paths, environment variables, and required capabilities clearly.

### Secure by default

The repo should avoid surprising privilege assumptions, document risky modes, and isolate optional elevated behavior behind explicit configuration.

### Extensible by design

Adding a new base image, agent provider, or tool pack should feel like adding a definition, not rewriting the system.

### Observable from day one

Builds, containers, startup flows, health checks, logs, metadata, and image provenance should all be first-class concerns.

## Scope of v1

The first meaningful version should support:

* a manifest-driven matrix model
* generated Dockerfiles committed to the repo
* CI build and publish workflows
* common tools layer
* base images for Node/Bun, Python, Dotnet, Rust, C/C++, and Haskell
* combo images such as Node/Python/Dotnet
* agent overlays for Claude Code, OpenClaw, Gemini CLI, OpenCode, and similar pluggable entries
* optional tool packs layered above base or combo images
* image metadata labeling and manifest output
* multiple example Compose setups
* local generation and validation scripts
* documentation for extending the matrix

## Non-goals for v1

To keep the first implementation sane, v1 should not attempt to:

* build a full web dashboard
* abstract every possible secret or auth flow behind a universal UI
* support every agent on day one
* become a complete runtime orchestrator by itself
* hide the underlying Docker reality from advanced users

It should be a strong container catalog and generation system first.

## Success criteria

AgentContainers is successful when:

* adding a new image combination is primarily a configuration change
* Dockerfiles are consistently generated and reproducible
* users can discover images through predictable tags and docs
* Compose examples actually work end to end
* the repo remains understandable as it scales
* multiple agent providers can coexist without bespoke chaos

---

# DESIGN.md

## Overview

AgentContainers is a matrix-driven container generation system built around layered definitions and deterministic generation.

At a high level, the repository has five major concerns:

1. image definition
2. image generation
3. image validation
4. image publishing
5. runtime composition

The design should separate these concerns cleanly so that the repo stays maintainable as the number of combinations grows.

## Conceptual model

### Layer types

The system should model images as compositions of ordered layers.

#### 1. Common tools layer

This contains utilities that are broadly useful across nearly all agent containers.

Examples:

* git
* curl
* wget
* less
* sudo
* jq
* nano
* vim
* unzip
* gnupg2
* man-db
* ripgrep
* fd
* procps
* ca-certificates
* bash-completion
* zsh
* tar
* xz-utils
* tree
* htop
* openssh-client

This layer should be versioned and centrally defined so changes can be applied consistently.

#### 2. Base runtime layer

A base runtime is a primary language or platform environment.

Examples:

* node-bun
* python
  n- dotnet
* rust
* cpp
* haskell

Each base runtime declares:

* its upstream image source
* package manager strategy
* runtime install steps
* shell assumptions
* path exports
* cache locations
* validation commands

#### 3. Combo runtime layer

A combo runtime is a predefined union of multiple base runtimes.

Examples:

* node-py
* node-py-dotnet
* rust-py
* dotnet-py

Combo runtimes should be derived from reusable fragments, not duplicated handwritten Dockerfiles.

#### 4. Agent overlay layer

An agent overlay installs and configures one or more agent providers on top of a runtime.

Examples:

* claude
* openclaw
* opencode
* gemini
* codex-compatible shell tooling

An overlay should describe:

* install mechanism
* runtime dependency requirements
* expected environment variables
* default config directories
* health probe strategy
* optional shell helper scripts
* plugin extension hooks
* known caveats

#### 5. Tool pack overlay layer

A tool pack adds optional utilities tailored to a use case.

Examples:

* gitops-pack
* gh-azure-pack
* discord-pack
* headroom-pack
* build-pack
* database-pack
* reverse-engineering-pack

A tool pack should be attachable to either a base image or an agent image.

## Definition model

The repo should use a small set of declarative manifest files, likely YAML or JSON, to describe the matrix.

Suggested top-level definition groups:

* `common-tools/`
* `bases/`
* `combos/`
* `agents/`
* `toolpacks/`
* `composes/`
* `profiles/`

### Example logical schema

Each image definition should normalize into an internal model like:

* id
* displayName
* family
* baseFrom
* inheritedLayers
* packages
* env
* mounts
* labels
* features
* validation
* healthChecks
* composeCapabilities
* publish
* tags

A generator should resolve inheritance and composition into a fully materialized build model.

## Generation pipeline

The system should generate repository artifacts deterministically.

### Inputs

* common tools definitions
* base runtime manifests
* combo manifests
* agent manifests
* tool pack manifests
* tag policy configuration
* template files

### Outputs

* generated Dockerfiles
* generated metadata manifests
* generated README snippets or docs tables
* generated Compose fragments or examples
* generated validation scripts
* generated SBOM or provenance hooks where available

### Determinism rules

* generation order should be stable
* output paths should be predictable
* formatting should be normalized
* redundant regeneration should be minimized
* diffs should be human-readable

## Proposed repo layout

```text
AgentContainers/
  definitions/
    common-tools/
    bases/
    combos/
    agents/
    toolpacks/
    compose-stacks/
    tag-policies/
  templates/
    dockerfiles/
    compose/
    docs/
    scripts/
  generated/
    dockerfiles/
    compose/
    manifests/
    docs/
  src/
    AgentContainers.Generator/
    AgentContainers.Core/
    AgentContainers.Validation/
  scripts/
    generate.ps1
    generate.sh
    validate.ps1
    validate.sh
    build-local.ps1
    build-local.sh
    publish.ps1
    publish.sh
  compose/
    examples/
    fragments/
  docs/
    images/
    agents/
    toolpacks/
    compose/
  .github/
    workflows/
```

The exact implementation language can be decided later, but the repo shape should keep source definitions separate from generated outputs.

## Static Dockerfiles and generated sources

The repo should commit generated Dockerfiles.

That is important for several reasons:

* users can inspect the exact build instructions without running the generator
* CI can validate drift between definitions and generated artifacts
* pull requests show concrete operational changes
* users who do not want the generator can still consume the static files

Recommended pattern:

* `definitions/` is the source of truth
* `generated/dockerfiles/` contains committed output
* CI fails if generated artifacts are out of date

## Tagging strategy

Tagging needs to be machine-friendly and human-readable.

Suggested canonical tag format:

`<runtime-family>-<agent-set>[-<toolpack-set>]:<version>`

Examples:

* `node-py-dotnet-claude-openclaw:latest`
* `node-bun-claude:2026.04.0`
* `python-openclaw-headroom:latest`
* `rust-gemini-buildtools:latest`

Recommended metadata dimensions:

* runtime family
* included agents
* included tool packs
* distro/base family
* image version
* source commit SHA
* generated manifest version

## Build strategy

The build system should support both local and CI-driven flows.

### Local flows

* generate everything
* validate definitions
* build one image
* build one image family
* build all images for a profile
* smoke test one compose stack

### CI flows

* lint definitions
* regenerate and verify no drift
* build changed images
* run smoke tests
* scan vulnerabilities
* publish images
* publish generated docs

## Validation model

Every image should have automated validation.

Validation should happen at multiple layers.

### Definition validation

Checks the manifests for schema correctness, duplicate IDs, invalid references, circular dependencies, and tag collisions.

### Build validation

Ensures generated Dockerfiles build successfully.

### Runtime validation

Runs commands inside the built image to ensure:

* shells open properly
* common tools exist
* runtimes report versions
* agents are callable
* important env variables or paths are present

### Compose validation

Launches example topologies and checks readiness, logs, and health endpoints where applicable.

## Compose architecture

Compose support is not just an afterthought. It is one of the differentiators.

The repo should ship with realistic examples.

### Example stack categories

#### Solo stack

One image, one agent, mounted workspace, persistent config.

#### Dual agent stack

Claude Code in one container and OpenClaw in another with shared workspace and separate state volumes.

#### Gateway stack

OpenClaw plus supporting services and optional Headroom integration.

#### Collaboration stack

Multiple agent containers, optional Discord integration, shared project workspace, shared logs, and a routing sidecar.

#### Tooling stack

Agent containers plus service containers for databases, message brokers, or local model runtimes.

## Extensibility points

The repo must advertise clear extension points.

### New base image

Add a new manifest and optional install fragments.

### New agent overlay

Add install logic, env schema, health semantics, docs metadata, and validation rules.

### New tool pack

Add package definitions, optional entrypoints, and compatibility constraints.

### New Compose stack

Compose stacks should be definable from reusable fragments so examples do not devolve into giant duplicated YAML files.

### Custom org profiles

Users should be able to define opinionated bundles for their organization.

Examples:

* secure-enterprise-profile
* local-experimentation-profile
* csharp-platform-profile

## Observability design

Observability should be included at both build time and runtime.

### Build-time observability

* generation reports
* build matrix summaries
* publish summaries
* artifact manifests
* vulnerability scan output

### Runtime observability

* container labels
* structured startup logs
* optional health endpoints or commands
* optional diagnostics scripts
* Compose readiness semantics

### Provenance and supply chain

Where feasible, published images should support:

* SBOM generation
* OCI labels
* provenance attestations
* vulnerability scanning integration

## Security design

Security should be explicit, not implied.

The design should account for:

* least privilege defaults
* documented privileged modes when required
* volume mount clarity
* secret injection patterns via environment and files
* avoidance of hard-coded credentials
* clear distinction between dev convenience and hardened modes

Some agents may need looser permissions or shell helpers. The repo should represent those as deliberate profile choices, not invisible defaults.

## Documentation design

Docs should be generated where helpful but written where judgment matters.

Recommended doc set:

* VISION.md
* DESIGN.md
* PLAN.md
* CONTRIBUTING.md
* IMAGE-CATALOG.md
* EXTENSIBILITY.md
* COMPOSE-GUIDE.md
* SECURITY.md
* OBSERVABILITY.md
* TAGGING.md

---

# PLAN.md

## Delivery strategy

The implementation should proceed in small vertical slices. The first milestone should prove that the matrix model works end to end before chasing every possible image combination.

## Phase 0. Repo bootstrap

### Goals

* create repository structure
* choose implementation language for generator
* establish linting, formatting, and CI basics
* define contribution conventions

### Deliverables

* initial repo scaffold
* definition schema placeholders
* generation script entrypoints
* baseline CI workflow
* initial docs set

## Phase 1. Core matrix model

### Goals

* model common tools, bases, combos, agents, and tool packs
* define schemas and reference validation
* implement deterministic generation

### Deliverables

* manifest schema definitions
* generator core
* normalized internal model
* generated Dockerfile output folder
* drift detection in CI

### Acceptance criteria

* invalid references fail clearly
* generation output is deterministic
* changed definitions produce predictable diffs
* generator can emit at least a small set of sample Dockerfiles

## Phase 2. Foundational image families

### Goals

* ship common tools layer
* implement primary base images
* implement first combo images

### Deliverables

* Node/Bun base image
* Python base image
* Dotnet base image
* Rust base image
* C/C++ base image
* Haskell base image
* Node/Python/Dotnet combo image

### Acceptance criteria

* each image builds locally and in CI
* each image passes runtime validation checks
* tags are generated consistently
* docs list supported runtimes and contents

## Phase 3. Agent overlays

### Goals

* install and validate initial agent providers
* formalize overlay contract

### Initial target overlays

* Claude Code
* OpenClaw
* OpenCode
* Gemini CLI

### Deliverables

* agent manifests
* agent install fragments
* overlay validation tests
* provider-specific documentation

### Acceptance criteria

* each overlay documents required env vars and persistence paths
* each overlay can be combined with at least one supported base image
* failures due to missing runtime dependencies are caught during generation or validation

## Phase 4. Tool packs

### Goals

* support optional layered tool packs
* prove that tool packs can compose without image explosion chaos

### Candidate tool packs

* headroom pack
* gh/azure/devops pack
* discord pack
* build tools pack
* diagnostics pack

### Acceptance criteria

* tool packs are declarative and reusable
* compatibility constraints are enforced
* docs explain exactly what each pack adds

## Phase 5. Compose topologies

### Goals

* ship realistic Compose examples
* prove sophisticated multi-agent local setups are easy to launch

### Initial examples

* single Claude Code workspace
* single OpenClaw gateway workspace
* Claude Code plus OpenClaw shared workspace
* OpenClaw plus Headroom
* Claude Code plus OpenClaw plus Headroom plus Discord integration

### Acceptance criteria

* each example launches with documented prerequisites
* health and readiness checks are included where possible
* persistent state and workspace mounts are documented clearly

## Phase 6. Publishing and cataloging

### Goals

* automate image publication
* produce a usable image catalog
  n

### Deliverables

* publish workflows
* registry push logic
* generated image matrix catalog
* changelog/release notes model

### Acceptance criteria

* changed images can be selectively rebuilt and published
* published images carry OCI labels and useful metadata
* the catalog stays synchronized with definitions

## Phase 7. Hardening and scale-out

### Goals

* improve security, provenance, and maintainability
* enable community or team extension without losing control

### Deliverables

* vulnerability scanning integration
* SBOM support
* provenance support where feasible
* policy checks for risky definitions
* profile-based publishing subsets

## Work breakdown structure

### Track A. Generator and schema

* definition schema design
* inheritance resolution
* generation engine
* output formatting
* drift detection

### Track B. Image authoring

* common package strategy
* base image definitions
* combo image definitions
* agent overlay definitions
* tool pack definitions

### Track C. Validation and test harness

* schema validation
* build smoke tests
* runtime smoke tests
* compose smoke tests

### Track D. CI/CD and release engineering

* matrix workflows
* caching strategy
* selective build logic
* registry publishing
* artifact retention

### Track E. Documentation and examples

* getting started guide
* extension guide
* image catalog
* Compose examples
* troubleshooting guide

## Initial decisions to make early

These should be resolved in the first implementation pass.

### 1. Generator language

A .NET implementation would fit well if the goal is strong modeling, validation, and maintainable tooling. Python or TypeScript are also viable, but .NET would likely provide a strong long-term core for schema-heavy and deterministic generation work.

### 2. Definition format

YAML is friendlier for contributors. JSON is stricter and simpler for machine tooling. A hybrid strategy is possible, but one should be chosen intentionally.

### 3. Output location

Generated artifacts should live in a stable, clearly machine-owned path such as `generated/`.

### 4. Build scope control

The repo needs a concept of profiles or filters so users are not forced to build the full universe every time.

### 5. Compatibility rules

The generator must understand invalid combinations, such as an agent requiring Node on an image that does not provide it.

## Risks

### Image explosion

A naïve matrix can create too many combinations to build and maintain. The design should support explicit publish filters and curated supported sets.

### Drift between generated and intended behavior

If the generation logic becomes too magical, contributors may stop trusting it. Static output and drift checks are essential.

### Agent volatility

Agent installers and assumptions change. Overlays must isolate this volatility so it does not contaminate the whole repo.

### Compose complexity

Multi-container topologies can become fragile quickly. Examples should start small and build toward richer stacks.

## Recommended first milestone

The first end-to-end milestone should intentionally stay narrow.

### First milestone scope

* common tools definition
* Node/Bun base
* Python base
* Dotnet base
* Node/Python/Dotnet combo
* Claude Code overlay
* OpenClaw overlay
* one Headroom tool pack
* one Compose example for Claude plus OpenClaw plus Headroom
* generator with committed static Dockerfiles
* CI drift and build validation

That slice is large enough to prove the architecture, but small enough to keep the repo from collapsing under its own ambition.

## Suggested next files after this pack

* REPO-STRUCTURE.md
* IMAGE-CATALOG.md
* EXTENSIBILITY.md
* COMPOSE-GUIDE.md
* SECURITY.md
* OBSERVABILITY.md
* CONTRIBUTING.md
* MANIFEST-SCHEMA.md
* TAGGING.md
