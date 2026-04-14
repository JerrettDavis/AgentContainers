# MANIFEST-MODEL.md

## AgentContainers — Manifest Model and Schema Design

---

## 1. Overview

The manifest model is the **definition layer** of AgentContainers. Every image the system can generate is described by one or more manifest files. The generator reads manifests, resolves composition and inheritance, evaluates compatibility, and renders templates.

**Format:** YAML (`.yaml` extension enforced)
**Validation:** JSON Schema (files in `schemas/`)
**Location:** `definitions/`

---

## 2. Entity Types

The system defines six top-level entity types. Each maps to a subdirectory under `definitions/` and a corresponding JSON Schema file under `schemas/`.

| Entity Type | Directory | Schema File | Purpose |
|---|---|---|---|
| Common Tools | `definitions/common-tools/` | `schemas/common-tools.schema.json` | Universal utility packages |
| Base Runtime | `definitions/bases/` | `schemas/base.schema.json` | Single-language platform |
| Combo Runtime | `definitions/combos/` | `schemas/combo.schema.json` | Multi-language union |
| Agent Overlay | `definitions/agents/` | `schemas/agent.schema.json` | Agent provider install + config |
| Tool Pack | `definitions/toolpacks/` | `schemas/toolpack.schema.json` | Optional additive utilities |
| Compose Stack | `definitions/compose-stacks/` | `schemas/compose-stack.schema.json` | Multi-service Compose topology |
| Tag Policy | `definitions/tag-policies/` | `schemas/tag-policy.schema.json` | Tag naming and publish rules |

---

## 3. Common Fields (All Entities)

Every manifest must include the following top-level fields:

```yaml
id: string                # Unique identifier, kebab-case, used in paths and tags
displayName: string       # Human-readable name
version: string           # Manifest schema version (e.g., "v1")
description: string       # One-line description
maintainers:              # Optional list
  - name: string
    github: string
labels:                   # Free-form key/value metadata emitted as OCI labels
  key: value
```

---

## 4. Common Tools Manifest

**Schema file:** `schemas/common-tools.schema.json`

```yaml
# definitions/common-tools/default.yaml
id: default
displayName: Default Common Tools
version: v1
description: Universal shell utilities installed on every AgentContainers image.

packages:
  apt:
    - git
    - curl
    - wget
    - less
    - sudo
    - jq
    - nano
    - vim
    - unzip
    - gnupg2
    - man-db
    - ripgrep
    - fd-find
    - procps
    - ca-certificates
    - bash-completion
    - zsh
    - tar
    - xz-utils
    - tree
    - htop
    - openssh-client

post_install:
  # Commands run after apt install, before anything else
  - command: "ln -sf /usr/bin/fdfind /usr/local/bin/fd"
    description: "Alias fd-find to fd"

validation:
  commands:
    - "git --version"
    - "curl --version"
    - "jq --version"
    - "rg --version"
    - "fd --version"
```

---

## 5. Base Runtime Manifest

**Schema file:** `schemas/base.schema.json`

```yaml
# definitions/bases/node-bun.yaml
id: node-bun
displayName: Node.js + Bun
version: v1
description: Node.js LTS with Bun runtime and npm/yarn/pnpm.
family: javascript

from:
  image: "node:22-bookworm-slim"
  # digest pinning optional; recommended for production builds
  # digest: "sha256:..."

common_tools: default      # Reference to common-tools manifest ID

provides:
  - node
  - "node>=22"
  - npm
  - bun
  - "bun>=1.0"
  - javascript

install:
  steps:
    - description: "Install Bun"
      run: |
        export BUN_INSTALL=/usr/local/bun
        mkdir -p "$BUN_INSTALL"
        curl -fsSL https://bun.sh/install | bash
        ln -sf /usr/local/bun/bin/bun /usr/local/bin/bun
        ln -sf /usr/local/bun/bin/bunx /usr/local/bin/bunx
        echo 'export BUN_INSTALL=/usr/local/bun' >> /etc/profile.d/bun.sh
        echo 'export PATH="/usr/local/bun/bin:$PATH"' >> /etc/profile.d/bun.sh

env:
  - name: NODE_ENV
    default: development
    description: Node.js environment mode
    sensitive: false
  - name: BUN_INSTALL
    default: /usr/local/bun
    description: Bun install path
    sensitive: false

mounts: []

user:
  name: dev
  uid: 1000
  gid: 1000
  sudo: true        # Available but not default

shell: bash         # Default shell; override to zsh or sh

healthcheck:
  test: ["CMD", "node", "--version"]
  interval: 30s
  timeout: 5s
  retries: 3

validation:
  commands:
    - "node --version"
    - "npm --version"
    - "bun --version"
    - "which git"

resource_hints:
  image_size_class: medium    # small | medium | large
  memory_minimum_mb: 512
```

---

## 6. Combo Runtime Manifest

**Schema file:** `schemas/combo.schema.json`

```yaml
# definitions/combos/node-py-dotnet.yaml
id: node-py-dotnet
displayName: Node.js + Python + .NET
version: v1
description: Tri-language combo image for full-stack and agent tooling.

bases:
  - id: node-bun
    order: 1
  - id: python
    order: 2
  - id: dotnet
    order: 3

# The combo provides the union of all constituent base capabilities
# (auto-derived; list here is for documentation clarity)
provides:
  - node
  - "node>=22"
  - python
  - "python>=3.12"
  - dotnet
  - "dotnet>=8"
  - bun

cross_runtime_utilities:
  apt:
    - pipx           # Python tool installer usable in Node-primary context

validation:
  commands:
    - "node --version"
    - "python3 --version"
    - "dotnet --version"
    - "bun --version"

resource_hints:
  image_size_class: large
  memory_minimum_mb: 1024
```

---

## 7. Agent Overlay Manifest

**Schema file:** `schemas/agent.schema.json`

```yaml
# definitions/agents/claude.yaml
id: claude
displayName: Claude Code
version: v1
description: Anthropic Claude Code CLI agent overlay.

requires:
  - "node>=18"       # Capability tokens; must be provided by base or combo

conflicts: []

install:
  method: npm_global
  package: "@anthropic-ai/claude-code"
  version: latest    # Pin to a specific version for reproducibility in production
  post_install:
    - description: "Verify installation"
      run: "claude --version"

env:
  - name: ANTHROPIC_API_KEY
    required: true
    description: Anthropic API key for authentication
    sensitive: true
    inject_from: environment
  - name: CLAUDE_CONFIG_DIR
    required: false
    default: "/home/dev/.config/claude"
    description: Claude configuration directory
    sensitive: false
  - name: CLAUDE_WORKSPACE
    required: false
    default: "/workspace"
    description: Default workspace path
    sensitive: false

mounts:
  - name: workspace
    container_path: /workspace
    description: Project workspace (bind mount from host)
    required: false
    suggested_host_path: "."
  - name: claude-config
    container_path: /home/dev/.config/claude
    description: Claude configuration and state persistence
    required: false
    suggested_host_path: "~/.config/claude"

healthcheck:
  test: ["CMD-SHELL", "claude --version || exit 1"]
  interval: 30s
  timeout: 10s
  retries: 3
  start_period: 15s

validation:
  commands:
    - "claude --version"
    - "test -d /home/dev/.config || mkdir -p /home/dev/.config"

shell_helpers:
  # Optional scripts injected into /usr/local/bin/ in the image
  - name: claude-workspace
    description: "Sets up a Claude workspace with correct permissions"

known_caveats:
  - "Requires ANTHROPIC_API_KEY to be injected at runtime; not set at build time"
  - "Running as non-root user dev; workspace mount must be owned by UID 1000"

privileged_modes: []   # No elevated privileges required

compose_capabilities:
  # Metadata used when this agent is referenced in a compose-stack definition
  service_name_default: claude
  ports: []
  networks: [agent-net]
```

```yaml
# definitions/agents/openclaw.yaml
id: openclaw
displayName: OpenClaw
version: v1
description: OpenClaw agent with API service exposure and compose-friendly deployment.

requires:
  - node

conflicts: []

install:
  method: npm_global
  package: "openclaw"
  version: latest

env:
  - name: OPENCLAW_API_KEY
    required: true
    description: OpenClaw API key
    sensitive: true
    inject_from: environment
  - name: OPENCLAW_PORT
    required: false
    default: "3000"
    description: Port for OpenClaw API service
    sensitive: false
  - name: OPENCLAW_CONFIG_DIR
    required: false
    default: "/home/dev/.config/openclaw"
    sensitive: false

mounts:
  - name: workspace
    container_path: /workspace
    required: false
    suggested_host_path: "."
  - name: openclaw-config
    container_path: /home/dev/.config/openclaw
    required: false
    suggested_host_path: "~/.config/openclaw"

healthcheck:
  test: ["CMD-SHELL", "curl -sf http://localhost:${OPENCLAW_PORT:-3000}/health || exit 1"]
  interval: 15s
  timeout: 5s
  retries: 5
  start_period: 20s

validation:
  commands:
    - "openclaw --version"

known_caveats:
  - "OpenClaw exposes an HTTP API; ensure port is not inadvertently exposed externally in production"

compose_capabilities:
  service_name_default: openclaw
  ports:
    - "${OPENCLAW_PORT:-3000}:3000"
  networks: [agent-net]
```

---

## 8. Tool Pack Manifest

**Schema file:** `schemas/toolpack.schema.json`

```yaml
# definitions/toolpacks/headroom.yaml
id: headroom
displayName: Headroom Proxy Pack
version: v1
description: |
  Adds the Headroom token-optimization proxy client to agent images.
  Headroom runs as a sidecar service; this pack configures agents to route through it.

compatible_with:
  bases: [node-bun, python, dotnet, node-py-dotnet]
  agents: [claude, openclaw]    # Leave empty to allow any

conflicts: []

# Packages installed into the agent image (the client/config side)
install:
  method: npm_global
  package: "headroom-cli"
  version: latest
  post_install:
    - run: "headroom --version"

env:
  - name: HEADROOM_PROXY_URL
    required: false
    default: "http://headroom:8080"
    description: URL of the Headroom sidecar service
    sensitive: false
  - name: HEADROOM_API_KEY
    required: false
    description: Optional authentication token for Headroom service
    sensitive: true
    inject_from: environment

# The sidecar service definition is generated into compose fragments
sidecar:
  enabled: true
  service_definition: "headroom-service"    # References a compose-stack fragment
  depends_on_agent: true    # Agents should declare dependency on headroom health

validation:
  commands:
    - "headroom --version"

compose_capabilities:
  sidecar_service: headroom
  networks: [agent-net]
```

---

## 9. Compose Stack Manifest

**Schema file:** `schemas/compose-stack.schema.json`

```yaml
# definitions/compose-stacks/gateway-headroom.yaml
id: gateway-headroom
displayName: Gateway Stack with Headroom
version: v1
description: OpenClaw as gateway agent with Headroom proxy sidecar and optional Claude.

services:
  - id: openclaw
    agent: openclaw
    base: node-bun
    toolpacks: [headroom]
    depends_on:
      - id: headroom
        condition: service_healthy
    env_overrides: {}

  - id: headroom
    type: sidecar
    toolpack: headroom
    image: "headroom/headroom:latest"
    ports:
      - "8080:8080"
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:8080/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5

  - id: claude
    agent: claude
    base: node-bun
    optional: true    # Only included if profile includes it
    depends_on:
      - id: headroom
        condition: service_healthy

networks:
  - name: agent-net
    driver: bridge

volumes:
  - name: workspace
    description: Shared project workspace
  - name: openclaw-state
  - name: claude-state
```

---

## 10. Inheritance and Resolution Rules

### Resolution Order

1. Generator loads all manifests and builds an entity registry
2. For each requested combination, resolution proceeds bottom-up through the layer stack
3. Each layer's fields are merged onto the cumulative model using the following rules:

| Field Type | Merge Strategy |
|---|---|
| `packages` | Union (no duplicates) |
| `env` | Overlay; later layer wins on same name |
| `mounts` | Union; duplicate `container_path` is an error |
| `labels` | Merge; later layer wins on same key |
| `validation.commands` | Concatenate in layer order |
| `provides` | Union (auto-computed) |
| `requires` | Intersection check against `provides` of lower layers |
| `healthcheck` | Outermost layer (agent/toolpack) wins |

### Circular Dependency Detection

If combo A includes base B, and base B references combo A (directly or transitively), the generator raises an error at load time.

### Compatibility Check

After resolution, for each combination:
1. Collect all `provides` tokens from the base/combo layer
2. For each agent and toolpack, check that all `requires` entries are satisfied
3. Check all `conflicts` entries against other layers in the same combination
4. Fail fast with a descriptive error: `[agent:claude] requires [node>=18] but [base:python] provides [python, python>=3.12]; missing: node>=18`

---

## 11. Extensibility Points

### Adding a New Manifest Type

1. Define a new JSON Schema in `schemas/`
2. Add a new `definitions/<type>/` directory
3. Implement a corresponding C# model in `AgentContainers.Core`
4. Add resolution logic if the type participates in layer composition

### Overriding Common Tools

An image manifest can specify `common_tools: minimal` if a `minimal` variant is defined in `definitions/common-tools/minimal.yaml`. This allows size-optimized images without modifying the main definition.

### Private Definition Directories

The generator CLI accepts a `--definitions-path` flag. Organizations can maintain private definition directories and run the generator against them without forking the repo. Private manifests can reference public base entity IDs by ID.

### Schema Versioning

The `version:` field in each manifest declares which schema revision it targets. The generator validates against the declared version. Old manifests remain valid as long as their declared schema version is supported.

---

## 12. Validation Rules Summary

| Rule | Type | Error Level |
|---|---|---|
| Required fields present | Schema | Error |
| ID is unique within type | Reference | Error |
| All referenced IDs exist | Reference | Error |
| No circular dependencies | Graph | Error |
| All `requires` are satisfied | Compatibility | Error |
| No `conflicts` violated | Compatibility | Error |
| Duplicate `container_path` in mounts | Merge | Error |
| `sensitive: true` env without `inject_from` | Security | Warning |
| `privileged_modes` non-empty without rationale | Security | Warning |
| `version` pinned to `latest` | Reproducibility | Info |
| Image size class `large` on a solo stack | Resource | Info |
