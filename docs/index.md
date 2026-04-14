# AgentContainers documentation

AgentContainers is a manifest-driven system for building, validating, and publishing agent-ready container images and compose stacks from a small set of YAML definitions.

## What you can do here

- Understand the **runtime, agent, tool-pack, and compose matrix**
- Learn the **generator workflow** for validating and regenerating artifacts
- Use the **Dockerfile and compose guidance** to consume generated outputs directly
- Follow **examples and user guides** for local development and operations
- Browse the **.NET API reference** for the generator and manifest model

## Start here

1. [Getting started](articles/getting-started.md)
2. [User guide](articles/user-guide.md)
3. [Dockerfiles and generated artifacts](articles/dockerfiles.md)
4. [Dockerfile matrix](articles/dockerfile-matrix.md)
5. [Examples](articles/examples.md)

## Reference

- [Generator CLI reference](articles/generator-cli.md)
- [Extending the matrix](articles/extending-the-matrix.md)
- [Environment variable contract](ENV-CONTRACT.md)
- [End-to-end validation](e2e-testing.md)
- [Architecture and planning docs](plans/README.md)
- [API reference](api/toc.yml)

## Validation model

The repo validates documentation as part of normal engineering flow:

- `.NET` build and test validation
- manifest validation and generated-artifact drift detection
- DocFX site generation in CI
- Docker image and compose e2e validation

That keeps the docs aligned with the repo’s actual generator, manifests, and generated outputs.
