# docs/plans — Planning Document Index

This directory is the **canonical home for architectural and implementation planning** for AgentContainers.

The high-level product vision remains in the repo-root `VISION.md` file.
The documents here translate that vision into concrete, decision-oriented specifications ready for implementation.

---

## Document Map

| Document | Purpose |
|---|---|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | System architecture, layer model, generation pipeline, extension points, repo layout |
| [IMPLEMENTATION-PLAN.md](./IMPLEMENTATION-PLAN.md) | Phased delivery plan, milestones, acceptance criteria, work tracks, risks |
| [MANIFEST-MODEL.md](./MANIFEST-MODEL.md) | Manifest schema design, entity definitions, compatibility rules, inheritance model |
| [COMPOSE-STRATEGY.md](./COMPOSE-STRATEGY.md) | Compose service patterns, dependency model, stack shapes, health/observability/security |

---

## Key Architectural Decisions (Summary)

These are captured in detail within the documents above. This table is a quick-reference index.

| Decision | Choice | Rationale |
|---|---|---|
| Generator language | .NET 10 (C#) | Strong typing, rich serialization, deterministic output, testable |
| Definition format | YAML with JSON Schema validation | Human-friendly authoring, machine-strict validation |
| Template engine | Scriban | Lightweight .NET-native, logic-minimal, whitespace-predictable |
| Output location | `generated/` (committed) | Full transparency; CI can detect drift |
| Compatibility model | Explicit `requires` / `conflicts` in YAML | Early failure, no silent surprises |
| Compose architecture | Fragments + profiles, no mega-YAML | Composable, minimal duplication |
| Tag format | `<runtime-family>-<agents>[-<packs>]:<semver\|latest>` | Machine-parseable and human-readable |
| Secrets model | Environment variables and bind-mounted files only | No secrets baked into images |
| Provenance | OCI labels + optional SLSA/SBOM hooks | Supply-chain transparency from day one |
| Drift detection | CI regenerates and fails on diff | Definitions and generated artifacts stay in sync |

---

## v1 Scope Boundary

**In scope for v1:**
- YAML manifest schema and JSON Schema validator
- .NET 10 generator producing static Dockerfiles
- Common tools layer + 4 base runtimes (Node/Bun, Rust, Python, .NET)
- 2 combo images (node-py-dotnet, fullstack-polyglot)
- 4 agent overlays (Claude Code, Codex, Copilot, OpenClaw)
- 2 tool packs (headroom, devtools)
- 5 Compose stacks (solo-claude, solo-codex, solo-copilot, gateway-headroom, polyglot-devtools)
- CI drift detection + build validation
- OCI labels on all built images
- Basic image catalog documentation

**Deferred post-v1:**
- C/C++, Haskell, Go base images
- Additional agent overlays (Gemini, OpenCode)
- Additional tool packs (discord, gh-azure, database)
- SBOM / SLSA provenance attestation
- Web-based image catalog dashboard
- Universal secret/auth abstraction UI
- Full vulnerability scan gate (scan runs but does not gate by default in v1)

---

## Contributing to Plans

- Planning documents are written, not generated.
- Decisions recorded here should reference the specific gap they resolve.
- When a decision is superseded, update the document in place with a clear note rather than creating a parallel doc.
- Implementation code, scripts, and CI belong outside this directory.
