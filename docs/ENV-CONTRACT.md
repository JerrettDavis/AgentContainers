# Environment Variable Contract

This document describes how environment variables are managed in AgentContainers
compose stacks and Dockerfiles.

---

## Override Precedence (highest → lowest)

1. **`docker compose run -e VAR=val`** — per-invocation CLI override
2. **`environment:` in docker-compose.yaml** — compose-level values (uses `${VAR:-default}` syntax so host env wins)
3. **`env_file: .env`** — stack-level `.env` file (when declared in compose manifest)
4. **Dockerfile `ENV`** — build-time defaults (overridable via `--build-arg`)
5. **Manifest `default:`** — source of truth for defaults baked into generated output

---

## Env Var Categories

### 1. Sensitive / Secret Variables

Defined in manifests with `sensitive: true`. Never baked into images.

| Pattern | When | Example |
|---------|------|---------|
| `${VAR:?VAR is required}` | `required: true` | `ANTHROPIC_API_KEY` on claude agent |
| `${VAR:-}` | `required: false` | `ANTHROPIC_API_KEY` on headroom sidecar |

Operators must supply these via host environment or `.env` file.

### 2. Overridable Defaults (Compose)

Non-sensitive vars with a manifest `default:` value. Generated as:

```yaml
- HEADROOM_MODE=${HEADROOM_MODE:-token}
```

The host environment or `.env` file can override; if unset, the default applies.

### 3. Overridable Defaults (Dockerfile)

Non-sensitive base-image env vars use the `ARG`→`ENV` pattern:

```dockerfile
ARG PYTHONDONTWRITEBYTECODE=1
ENV PYTHONDONTWRITEBYTECODE=${PYTHONDONTWRITEBYTECODE}
```

Override at build time: `docker build --build-arg PYTHONDONTWRITEBYTECODE=0 .`

### 4. Hardcoded Defaults

Agent vars without the override pattern (e.g. `CLAUDE_CONFIG_DIR=/home/dev/.config/claude`)
are set directly in compose. These are structural paths that rarely need changing.

---

## Tool-Pack Env Var Model

Tool-packs define two env var lists:

| Field | Target | Purpose |
|-------|--------|---------|
| `env:` | Agent containers that include the pack | Client config (e.g. `HEADROOM_PROXY_URL`) |
| `sidecar_env:` | The sidecar container itself | Runtime config (e.g. `HEADROOM_HOST`, `HEADROOM_PORT`) |

### Headroom Sidecar Env Vars

| Variable | Default | Purpose |
|----------|---------|---------|
| `HEADROOM_HOST` | `0.0.0.0` | Bind address |
| `HEADROOM_PORT` | `8787` | Listen port |
| `HEADROOM_MODE` | `token` | Optimization mode |
| `HEADROOM_OPTIMIZE` | `true` | Enable optimization |
| `HEADROOM_MIN_TOKENS` | `500` | Compression threshold |
| `HEADROOM_SMART_ROUTING` | `true` | Content routing |
| `HEADROOM_CACHE_ENABLED` | `true` | Semantic caching |
| `HEADROOM_CACHE_TTL` | `3600` | Cache TTL (seconds) |
| `HEADROOM_RATE_LIMIT_ENABLED` | `true` | Rate limiting |
| `HEADROOM_RPM` | `60` | Requests/minute |
| `HEADROOM_TPM` | `100000` | Tokens/minute |
| `HEADROOM_BACKEND` | `anthropic` | LLM backend |
| `ANTHROPIC_API_KEY` | *(sensitive)* | API key pass-through |
| `OPENAI_API_KEY` | *(sensitive)* | API key pass-through |
| `HEADROOM_TELEMETRY` | `off` | Telemetry toggle |
| `HEADROOM_LOG_FILE` | *(none)* | JSONL log path |

### Headroom Client Env Vars (injected into agents)

| Variable | Default | Purpose |
|----------|---------|---------|
| `HEADROOM_PROXY_URL` | `http://headroom:8787` | Proxy URL for agent containers |

---

## `env_file` Support

Compose stack manifests can declare `env_file: .env`. When set, every service
in the generated stack gets an `env_file:` directive, enabling a single `.env`
file to supply all overrides and secrets for the stack.

Stacks without `env_file` (e.g. solo-claude) rely solely on host environment
injection.

---

## Adding Env Vars for a New Agent or Tool-Pack

1. Add entries to `env:` (or `sidecar_env:` for sidecars) in the manifest YAML
2. Set `sensitive: true` for secrets, `required: true` if mandatory
3. Provide a `default:` for non-sensitive vars that have sensible defaults
4. Run `dotnet run --project src/AgentContainers.Generator -- generate`
5. Verify generated compose and Dockerfile output
