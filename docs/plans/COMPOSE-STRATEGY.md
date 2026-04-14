# COMPOSE-STRATEGY.md

## AgentContainers — Compose Strategy

---

## 1. Principles

- **Fragments over monoliths.** Compose stacks are assembled from reusable service fragments, not hand-written monolithic files.
- **Profiles over flags.** Optional services are controlled by Docker Compose profiles, not commented-out blocks.
- **Health-first dependency chains.** Every dependency uses `condition: service_healthy`, never just `service_started`.
- **Explicit volumes.** Every volume used by a container is named and documented. Anonymous volumes are not used.
- **Secrets via environment.** API keys and tokens are injected via `.env` files or the host environment, never baked into images.
- **One network per stack.** Each Compose stack uses a single explicitly declared bridge network. Agents within a stack communicate by service name.

---

## 2. Fragment Model

The generator produces reusable Compose **fragments** under `generated/compose/fragments/`. Each fragment is a partial YAML file defining a single service, its healthcheck, volumes, and network membership.

Fragments are then **assembled** by the Compose stack generator into full `docker-compose.yaml` files under `generated/compose/examples/<stack-id>/`.

### Fragment Types

| Fragment Type | Source | Example |
|---|---|---|
| Agent service | Generated from agent manifest | `claude-service.yaml` |
| Sidecar service | Generated from toolpack manifest | `headroom-service.yaml` |
| Shared network | Standard template | `agent-net.yaml` |
| Shared volume | Generated from mount declarations | `workspace-volume.yaml` |

### Fragment Structure (agent-service example)

```yaml
# generated/compose/fragments/claude-service.yaml
# AUTO-GENERATED — do not edit by hand. Source: definitions/agents/claude.yaml
services:
  claude:
    image: ghcr.io/agentcontainers/node-bun-claude:latest
    environment:
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY:?ANTHROPIC_API_KEY is required}
      - CLAUDE_CONFIG_DIR=/home/dev/.config/claude
      - CLAUDE_WORKSPACE=/workspace
    volumes:
      - workspace:/workspace
      - claude-config:/home/dev/.config/claude
    networks:
      - agent-net
    healthcheck:
      test: ["CMD-SHELL", "claude --version || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 15s
    restart: unless-stopped
    user: "1000:1000"
```

---

## 3. Stack Shapes (v1)

### 3.1 Solo Stack — `solo-claude`

**Purpose:** Individual developer using Claude Code with a local workspace.

**Services:** 1

```
┌──────────────────────┐
│  claude              │
│  (node-bun-claude)   │
│  mounts: workspace   │
│  mounts: claude-cfg  │
└──────────────────────┘
```

**`docker-compose.yaml` structure:**

```yaml
# generated/compose/examples/solo-claude/docker-compose.yaml
# AUTO-GENERATED

version: "3.9"

services:
  claude:
    image: ghcr.io/agentcontainers/node-bun-claude:latest
    environment:
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY:?required}
      - CLAUDE_WORKSPACE=/workspace
    volumes:
      - workspace:/workspace
      - claude-config:/home/dev/.config/claude
    networks:
      - agent-net
    healthcheck:
      test: ["CMD-SHELL", "claude --version || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 15s
    restart: unless-stopped

networks:
  agent-net:
    driver: bridge

volumes:
  workspace:
  claude-config:
    driver: local
    driver_opts:
      type: none
      device: ${CLAUDE_CONFIG_HOST_PATH:-~/.config/claude}
      o: bind
```

**Required `.env.example`:**

```env
# solo-claude/.env.example
ANTHROPIC_API_KEY=          # Required: your Anthropic API key
CLAUDE_CONFIG_HOST_PATH=~/.config/claude   # Optional: override config mount source
```

---

### 3.2 Dual Agent Stack — `dual-agent`

**Purpose:** Claude Code and OpenClaw running side-by-side with shared workspace.

**Services:** 2

```
┌──────────────────┐    ┌──────────────────────┐
│  claude          │    │  openclaw             │
│  (node-bun-      │    │  (node-bun-openclaw)  │
│   claude)        │    │  ports: 3000          │
└────────┬─────────┘    └───────────┬───────────┘
         └──────────────────────────┘
                    agent-net
                  shared: workspace
```

**Dependency:** None between agents; both start independently and share the workspace volume.

**Notable details:**
- Each agent has its own config/state volume
- Shared `workspace` volume enables both agents to operate on the same files
- OpenClaw's API port is exposed to the host for external integration

**Required `.env.example`:**

```env
ANTHROPIC_API_KEY=
OPENCLAW_API_KEY=
OPENCLAW_PORT=3000
```

---

### 3.3 Gateway + Headroom Stack — `gateway-headroom`

**Purpose:** OpenClaw as gateway agent with Headroom proxy for token optimization.

**Services:** 2 required + 1 optional

```
                  ┌──────────────────────────┐
                  │  headroom                │
                  │  (headroom:latest)       │
                  │  port: 8080              │
                  │  healthcheck: /health    │
                  └───────────┬──────────────┘
                              │ condition: service_healthy
                  ┌───────────▼──────────────┐
                  │  openclaw                │
                  │  (node-bun-openclaw-     │
                  │   headroom)              │
                  │  HEADROOM_PROXY_URL=     │
                  │    http://headroom:8080  │
                  └──────────────────────────┘
             (optional)
                  ┌──────────────────────────┐
                  │  claude                  │
                  │  profile: claude         │
                  └──────────────────────────┘
```

**Dependency model:**

```yaml
services:
  openclaw:
    depends_on:
      headroom:
        condition: service_healthy
  claude:
    profiles: [claude]
    depends_on:
      headroom:
        condition: service_healthy
```

**Headroom service definition:**

```yaml
  headroom:
    image: headroom/headroom:latest
    environment:
      - HEADROOM_PORT=8080
      - HEADROOM_LOG_LEVEL=${HEADROOM_LOG_LEVEL:-info}
    volumes:
      - headroom-data:/data
    networks:
      - agent-net
    ports:
      - "8080:8080"
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:8080/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
    restart: unless-stopped
```

**Required `.env.example`:**

```env
OPENCLAW_API_KEY=
HEADROOM_LOG_LEVEL=info
# Optional: include claude service
# COMPOSE_PROFILES=claude
# ANTHROPIC_API_KEY=
```

---

## 4. Dependency Model

### Service Ordering

Docker Compose does not guarantee startup order without `depends_on`. All stacks must use explicit `depends_on` with `condition: service_healthy` for any service that another depends on.

**Rule:** If service A calls or connects to service B at startup, A must declare `depends_on: B: condition: service_healthy`.

### Healthcheck Requirements

Every service in a stack must declare a `healthcheck` block. Services without meaningful health probes must still declare a minimal check (e.g., process existence).

| Agent/Service | Healthcheck Strategy |
|---|---|
| Claude | `claude --version` exit code |
| OpenClaw | HTTP GET `/health` on agent port |
| Headroom | HTTP GET `/health` on proxy port |

### Readiness vs. Liveness

For v1, healthchecks serve dual purpose (readiness + liveness). Post-v1 consideration: separate probes if agents support it.

---

## 5. Network Architecture

### Single Bridge Network Per Stack

Each stack declares one `agent-net` bridge network. All services join it. This allows service-name DNS resolution between containers without exposing inter-service ports to the host.

### Port Exposure Policy

| Exposure Type | Rule |
|---|---|
| Agent API (OpenClaw) | Exposed to host; configurable via `${PORT}` env var |
| Proxy service (Headroom) | Exposed to host only if required for external tooling |
| Agent CLI (Claude) | Not a service; no port exposure |
| Internal only | Not published; use service DNS name |

### External Network Integration

For teams wiring AgentContainers into a larger Docker environment (e.g., a local dev platform with shared services), stacks support an `external: true` network declaration. This is configured in the compose-stack manifest via `networks.external: true`.

---

## 6. Volume Architecture

### Volume Types

| Type | Usage | Declaration |
|---|---|---|
| Named volume | Persistent state (config, agent data) | `volumes:` top-level |
| Bind mount | Live workspace, host config files | `device:` in volume options |
| Anonymous volume | Not used (explicit volumes only) | Prohibited |

### Standard Volume Names

Generated stacks use consistent names derived from service IDs:

- `workspace` — shared project workspace (bind mount recommended)
- `<agent-id>-config` — agent configuration persistence
- `<agent-id>-state` — agent runtime state
- `<service-id>-data` — sidecar service data

### Workspace Bind Mount Pattern

The `workspace` volume is a bind mount in all examples, defaulting to the current directory:

```yaml
volumes:
  workspace:
    driver: local
    driver_opts:
      type: none
      device: ${WORKSPACE_PATH:-.}
      o: bind
```

This lets users set `WORKSPACE_PATH` in their `.env` or environment to point at any local path.

---

## 7. Configuration and Secrets

### Configuration Layers (in precedence order)

1. Defaults baked into the image (non-sensitive only)
2. Values from the service's `environment:` block in Compose
3. Values from `.env` file in the stack directory
4. Host environment variables (passed through by Docker)

### Secrets Rule

No secret value is ever set in the `docker-compose.yaml` file itself. All sensitive values use the pattern:

```yaml
environment:
  - SECRET_KEY=${SECRET_KEY:?SECRET_KEY must be set in environment or .env}
```

The `:?` syntax causes Docker Compose to fail with a clear error if the variable is absent.

### `.env.example` Convention

Every Compose stack directory contains `.env.example` with every variable documented and sensible defaults. Users copy this to `.env` and fill in secrets. The `.env` file is gitignored; `.env.example` is committed.

---

## 8. Profiles Strategy

Docker Compose profiles allow optional services to be disabled by default and enabled explicitly.

### Standard Profile Names (v1)

| Profile | Services Included |
|---|---|
| `claude` | Claude agent service |
| `openclaw` | OpenClaw agent service |
| `headroom` | Headroom sidecar |
| `debug` | Additional diagnostics containers or verbose logging modes |

### Usage

```bash
# Start base stack only
docker compose up

# Start with Claude
docker compose --profile claude up

# Start full collaboration stack
docker compose --profile claude --profile headroom up
```

### Profile Documentation

Each stack's README must document which profiles exist and what they add. This is generated from the compose-stack manifest.

---

## 9. Observability in Compose

### Log Configuration

All services use the `json-file` logging driver with size limits:

```yaml
logging:
  driver: json-file
  options:
    max-size: "10m"
    max-file: "5"
```

### Structured Log Expectations

Agent overlays must emit structured startup log lines. Minimum required fields:

```json
{"event": "agent_started", "agent": "claude", "version": "1.2.3", "workspace": "/workspace"}
```

### Health Endpoint Aggregation (Post-v1)

For richer monitoring, a future stack variant may include a lightweight health aggregator container that polls all services and exposes a unified status page.

### Compose Watch (Dev Experience)

For development workflows, generated stacks include `develop.watch` configurations (Docker Compose Watch feature) so definition changes can trigger container recreation without manual intervention.

---

## 10. Security Considerations

### Non-Root Enforcement

All agent containers run as `user: "1000:1000"` in Compose. If the host workspace is owned by a different UID, the stack's README must document the required `chown` or `--user` override.

### Port Binding to Localhost

In development stacks, exposed ports bind to `127.0.0.1` by default:

```yaml
ports:
  - "127.0.0.1:${OPENCLAW_PORT:-3000}:3000"
```

Production deployments requiring external access must explicitly override this.

### Read-Only Root Filesystem (Post-v1)

Future hardened stack variants should use `read_only: true` with explicit `tmpfs` mounts for writable paths. v1 does not enforce this due to complexity with agent config directories.

### No Privileged Mode in Standard Stacks

Standard stacks do not use `privileged: true` or `cap_add`. If an agent requires elevated capabilities (e.g., Docker-in-Docker), that is a separate, clearly-documented profile.

---

## 11. Drift and Validation

### Compose Smoke Tests

The `scripts/validate.ps1 --compose <stack-id>` script:
1. Runs `docker compose config` to validate YAML correctness
2. Starts the stack with `docker compose up -d`
3. Polls health endpoints until all services are healthy or timeout
4. Runs declared validation commands inside each container
5. Tears down with `docker compose down -v`

### CI Integration

Smoke tests run on PRs that modify `generated/compose/`, `definitions/`, or `templates/`.

### Fragment Drift Detection

Like Dockerfile drift detection, Compose fragments are generated and diff-checked on every CI run. A modified definition that does not result in a generated-fragment update fails the CI check.
